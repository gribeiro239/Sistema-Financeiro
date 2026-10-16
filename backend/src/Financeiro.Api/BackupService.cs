using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Financeiro.Api;

public sealed class BackupOptions
{
    public bool Enabled { get; set; } = true;
    public string Directory { get; set; } = "App_Data/Backups";
    public string KeyFile { get; set; } = "App_Data/Backup-Key.key";
    public string EncryptionKey { get; set; } = "";
    public int IntervalHours { get; set; } = 24;
    public int RetentionCount { get; set; } = 14;
    public int MaxArchiveMegabytes { get; set; } = 512;
}

public sealed record BackupFileView(string Name, long Size, DateTimeOffset CreatedAt);
public sealed record BackupVerification(DateTimeOffset CreatedAt, int Files, int Attachments, string DatabaseIntegrity);
public sealed record BackupManifest(int Version, DateTimeOffset CreatedAt, Dictionary<string, string> Sha256);

// Attachment file mutations must finish before taking the SQLite snapshot and copying referenced files.
public sealed class BackupFileGate
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
}

public sealed class BackupService(IWebHostEnvironment environment, IConfiguration configuration,
    IOptions<BackupOptions> options, BackupFileGate gate, ILogger<BackupService> logger)
{
    private static readonly byte[] Magic = "NEXOBKP1"u8.ToArray();
    private readonly SemaphoreSlim operation = new(1, 1);
    private BackupOptions Settings => options.Value;
    public string BackupDirectory => Resolve(Settings.Directory);
    public DateTimeOffset? LastVerifiedAt { get; private set; }
    public bool LastAttemptFailed { get; private set; }
    public bool Enabled => Settings.Enabled;

    private string Resolve(string path) => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(environment.ContentRootPath, path));

    private void ValidateSettings()
    {
        if (Settings.IntervalHours is < 1 or > 8760 || Settings.RetentionCount is < 1 or > 1000 ||
            Settings.MaxArchiveMegabytes is < 1 or > 1024)
            throw new InvalidOperationException("Configuração de backup inválida.");
    }

    private byte[] GetKey(bool create)
    {
        if (!string.IsNullOrWhiteSpace(Settings.EncryptionKey))
        {
            var configured = Convert.FromBase64String(Settings.EncryptionKey);
            if (configured.Length != 32) throw new InvalidOperationException("Backups:EncryptionKey deve ter 32 bytes em Base64.");
            return configured;
        }
        var path = Resolve(Settings.KeyFile);
        if (!File.Exists(path) && create)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            output.Write(RandomNumberGenerator.GetBytes(32));
            output.Flush(true);
            logger.LogWarning("Chave de backup criada. Preserve uma cópia segura e separada do arquivo de chave; ele não faz parte dos backups.");
        }
        var key = File.ReadAllBytes(path);
        if (key.Length != 32) throw new InvalidOperationException("Arquivo de chave de backup inválido.");
        return key;
    }

    public IReadOnlyList<BackupFileView> List()
        => !Directory.Exists(BackupDirectory) ? [] : new DirectoryInfo(BackupDirectory).GetFiles("nexo-*.nexobak")
            .OrderByDescending(file => file.Name, StringComparer.Ordinal)
            .Select(file => new BackupFileView(file.Name, file.Length, file.CreationTimeUtc)).ToList();

    public string GetArchivePath(string name)
    {
        if (name != Path.GetFileName(name) || !name.StartsWith("nexo-", StringComparison.Ordinal) ||
            !name.EndsWith(".nexobak", StringComparison.Ordinal)) throw new ArgumentException("Nome de backup inválido.");
        var path = Path.Combine(BackupDirectory, name);
        if (!File.Exists(path)) throw new FileNotFoundException("Backup não encontrado.");
        return path;
    }

    public async Task<BackupFileView> CreateAsync(CancellationToken ct)
    {
        ValidateSettings();
        await operation.WaitAsync(ct);
        string? stage = null;
        try
        {
            Directory.CreateDirectory(BackupDirectory);
            stage = Path.Combine(BackupDirectory, ".stage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            var data = Path.Combine(stage, "App_Data"); Directory.CreateDirectory(data);
            var sourcePath = Resolve(configuration["Database:File"] ?? "App_Data/finance.db");
            await gate.Semaphore.WaitAsync(ct);
            try
            {
                using (var source = OpenDatabase(sourcePath))
                using (var destination = OpenDatabase(Path.Combine(data, "finance.db"), true))
                    source.BackupDatabase(destination);
                using var snapshot = OpenDatabase(Path.Combine(data, "finance.db"));
                foreach (var attachment in ReadAttachments(snapshot))
                {
                    ct.ThrowIfCancellationRequested();
                    var relative = AttachmentPath(attachment.WorkspaceId, attachment.Name);
                    var sourceFile = SafePath(environment.ContentRootPath, relative);
                    var destinationFile = SafePath(stage, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
                    File.Copy(sourceFile, destinationFile);
                }
                var keys = Resolve("App_Data/DataProtection-Keys");
                if (!Directory.Exists(keys) || !Directory.EnumerateFiles(keys, "key-*.xml").Any())
                    throw new InvalidOperationException("Não há chaves de autenticação para preservar.");
                var keyTarget = Path.Combine(data, "DataProtection-Keys"); Directory.CreateDirectory(keyTarget);
                foreach (var file in Directory.EnumerateFiles(keys, "key-*.xml")) File.Copy(file, Path.Combine(keyTarget, Path.GetFileName(file)));
            }
            finally { gate.Semaphore.Release(); }

            var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var path in Directory.EnumerateFiles(data, "*", SearchOption.AllDirectories))
                hashes.Add(Path.GetRelativePath(stage, path).Replace('\\', '/'), HashFile(path));
            var createdAt = DateTimeOffset.UtcNow;
            await File.WriteAllTextAsync(Path.Combine(stage, "manifest.json"), JsonSerializer.Serialize(new BackupManifest(1, createdAt, hashes)), ct);
            var zip = Path.Combine(stage, "payload.zip");
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                foreach (var relative in hashes.Keys.Append("manifest.json"))
                    archive.CreateEntryFromFile(SafePath(stage, relative), relative, CompressionLevel.Optimal);
            }
            if (new FileInfo(zip).Length > (long)Settings.MaxArchiveMegabytes * 1024 * 1024)
                throw new InvalidOperationException("Backup excedeu o limite de tamanho configurado.");
            var plaintext = await File.ReadAllBytesAsync(zip, ct);
            var ciphertext = new byte[plaintext.Length]; var nonce = RandomNumberGenerator.GetBytes(12); var tag = new byte[16];
            var key = GetKey(true);
            try { using var aes = new AesGcm(key, 16); aes.Encrypt(nonce, plaintext, ciphertext, tag, Magic); }
            finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plaintext); }
            var name = $"nexo-{createdAt:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}.nexobak";
            var temporary = Path.Combine(stage, "encrypted.tmp");
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await output.WriteAsync(Magic, ct); await output.WriteAsync(nonce, ct);
                await output.WriteAsync(tag, ct); await output.WriteAsync(ciphertext, ct); output.Flush(true);
            }
            // Test decrypt/extract/SQLite integrity before publishing or removing older archives.
            await VerifyAsync(temporary, ct);
            File.Move(temporary, Path.Combine(BackupDirectory, name));
            LastVerifiedAt = DateTimeOffset.UtcNow; LastAttemptFailed = false;
            foreach (var old in List().Skip(Settings.RetentionCount)) File.Delete(GetArchivePath(old.Name));
            logger.LogInformation("Backup criado e verificado: {Name}.", name);
            return List().First(file => file.Name == name);
        }
        catch { LastAttemptFailed = true; throw; }
        finally
        {
            if (stage is not null) DeleteStage(stage);
            operation.Release();
        }
    }

    public async Task<BackupVerification> VerifyAsync(string archivePath, CancellationToken ct)
    {
        ValidateSettings();
        Directory.CreateDirectory(BackupDirectory);
        var stage = Path.Combine(BackupDirectory, ".verify-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = await ExtractVerifiedAsync(archivePath, stage, ct);
            LastVerifiedAt = DateTimeOffset.UtcNow;
            return result;
        }
        finally { if (Directory.Exists(stage)) DeleteStage(stage); }
    }

    public async Task<BackupVerification> RestoreAsync(string archivePath, string target, CancellationToken ct)
    {
        var destination = Path.GetFullPath(target);
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new InvalidOperationException("A restauração exige uma pasta nova; nenhum dado existente será sobrescrito.");
        ValidateSettings();
        // Validate completely in private staging, then publish with an atomic directory move.
        var stage = destination + ".restore-" + Guid.NewGuid().ToString("N");
        try
        {
            var result = await ExtractVerifiedAsync(archivePath, stage, ct);
            Directory.Move(stage, destination);
            return result;
        }
        finally { if (Directory.Exists(stage)) DeleteStage(stage); }
    }

    private async Task<BackupVerification> ExtractVerifiedAsync(string archivePath, string target, CancellationToken ct)
    {
        var max = (long)Settings.MaxArchiveMegabytes * 1024 * 1024;
        if (new FileInfo(archivePath).Length > max + 36) throw new InvalidDataException("Backup excede o limite de tamanho.");
        var encrypted = await File.ReadAllBytesAsync(archivePath, ct);
        if (encrypted.Length < 36 || !encrypted.AsSpan(0, 8).SequenceEqual(Magic)) throw new InvalidDataException("Formato de backup inválido.");
        var plaintext = new byte[encrypted.Length - 36]; var key = GetKey(false);
        try { using var aes = new AesGcm(key, 16); aes.Decrypt(encrypted.AsSpan(8, 12), encrypted.AsSpan(36), encrypted.AsSpan(20, 16), plaintext, Magic); }
        finally { CryptographicOperations.ZeroMemory(key); }
        try
        {
            Directory.CreateDirectory(target);
            using var memory = new MemoryStream(plaintext, false);
            using var archive = new ZipArchive(memory, ZipArchiveMode.Read);
            long extracted = 0; var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                ct.ThrowIfCancellationRequested();
                if (!names.Add(entry.FullName) || (entry.FullName != "manifest.json" && !entry.FullName.StartsWith("App_Data/", StringComparison.Ordinal)))
                    throw new InvalidDataException("Conteúdo inesperado no backup.");
                extracted += entry.Length;
                if (extracted > max * 4) throw new InvalidDataException("Conteúdo descompactado excede o limite.");
                var path = SafePath(target, entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                entry.ExtractToFile(path);
            }
            var manifest = JsonSerializer.Deserialize<BackupManifest>(await File.ReadAllTextAsync(Path.Combine(target, "manifest.json"), ct))
                ?? throw new InvalidDataException("Manifesto ausente.");
            if (manifest.Version != 1 || names.Count != manifest.Sha256.Count + 1) throw new InvalidDataException("Manifesto inválido.");
            foreach (var pair in manifest.Sha256)
                if (!names.Contains(pair.Key) || HashFile(SafePath(target, pair.Key)) != pair.Value)
                    throw new InvalidDataException("Falha na integridade dos arquivos.");
            if (!names.Any(name => name.StartsWith("App_Data/DataProtection-Keys/key-", StringComparison.Ordinal) && name.EndsWith(".xml", StringComparison.Ordinal)))
                throw new InvalidDataException("Chaves de autenticação ausentes.");
            using var database = OpenDatabase(Path.Combine(target, "App_Data", "finance.db"));
            using var integrity = database.CreateCommand(); integrity.CommandText = "PRAGMA integrity_check";
            if ((string?)integrity.ExecuteScalar() != "ok") throw new InvalidDataException("Banco de dados inconsistente.");
            integrity.CommandText = "PRAGMA foreign_key_check";
            using (var violations = integrity.ExecuteReader())
                if (violations.Read()) throw new InvalidDataException("Banco possui referências inválidas.");
            var attachments = ReadAttachments(database);
            foreach (var attachment in attachments)
            {
                var path = SafePath(target, AttachmentPath(attachment.WorkspaceId, attachment.Name));
                if (!File.Exists(path) || new FileInfo(path).Length != attachment.Size) throw new InvalidDataException("Anexo ausente ou incompleto.");
            }
            return new(manifest.CreatedAt, manifest.Sha256.Count, attachments.Count, "ok");
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    private static SqliteConnection OpenDatabase(string path, bool create = false)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path,
            Mode = create ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open(); return connection;
    }

    private static List<(string WorkspaceId, string Name, long Size)> ReadAttachments(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT WorkspaceId, StoredFileName, Size FROM DocumentAttachments";
        using var reader = command.ExecuteReader();
        var result = new List<(string, string, long)>();
        while (reader.Read()) result.Add((reader.GetString(0), reader.GetString(1), reader.GetInt64(2)));
        return result;
    }

    private static string AttachmentPath(string workspaceId, string name)
    {
        if (name != Path.GetFileName(name) || name.Contains('\\') || name.Contains('/') || name.Contains(':'))
            throw new InvalidDataException("Nome de anexo inválido.");
        return $"App_Data/Attachments/{Guid.Parse(workspaceId):N}/{name}";
    }

    private static string SafePath(string root, string relative)
    {
        if (relative.Contains('\\') || relative.Contains(':') || relative.Split('/').Any(part => part is "" or "." or ".."))
            throw new InvalidDataException("Caminho inválido no backup.");
        var fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, relative));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Caminho fora do destino.");
        return fullPath;
    }

    private static string HashFile(string path)
    { using var input = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(input)); }

    private static void DeleteStage(string path)
    {
        // Only internally generated unique stage directories are deleted recursively.
        var full = Path.GetFullPath(path);
        var name = Path.GetFileName(full);
        if (!(name.StartsWith(".stage-", StringComparison.Ordinal) || name.StartsWith(".verify-", StringComparison.Ordinal) || name.Contains(".restore-", StringComparison.Ordinal)))
            throw new InvalidOperationException("Diretório temporário inválido.");
        Directory.Delete(full, true);
    }
}

public sealed class AutomaticBackupWorker(BackupService backups, IOptions<BackupOptions> options,
    ILogger<AutomaticBackupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!backups.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var latest = backups.List().FirstOrDefault();
                if (latest is null || DateTimeOffset.UtcNow - latest.CreatedAt >= TimeSpan.FromHours(options.Value.IntervalHours))
                    await backups.CreateAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogError("Backup automático falhou. Os backups anteriores foram preservados."); }
            try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}

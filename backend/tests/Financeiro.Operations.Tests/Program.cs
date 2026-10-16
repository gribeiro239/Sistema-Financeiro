using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Financeiro.Api;
using Financeiro.Application;
using Financeiro.Domain;
using Financeiro.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var ct = CancellationToken.None;
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../TestData", Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(Path.Combine(root, "App_Data", "DataProtection-Keys"));
var environment = new TestEnvironment(root);
var smtpOptions = new EmailOptions { Enabled = true, PublicUrl = "https://nexo.example", FromAddress = "nexo@example.com" };
var sender = new RecordingSender();
var services = new ServiceCollection();
services.AddLogging();
services.AddSingleton<IWebHostEnvironment>(environment);
services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(root, "App_Data", "DataProtection-Keys"))).SetApplicationName("NexoFinanceiro");
services.AddDbContext<FinanceDbContext>(options => options.UseSqlite($"Data Source={Path.Combine(root, "App_Data", "finance.db")};Pooling=False"));
services.AddSingleton<IOptions<EmailOptions>>(Options.Create(smtpOptions));
services.AddSingleton<IEmailSender>(sender);
services.AddScoped<EmailQueue>(); services.AddScoped<AuthenticationService>(); services.AddScoped<WorkspaceAccessService>();
await using var provider = services.BuildServiceProvider();
var protection = provider.GetRequiredService<IDataProtectionProvider>();
var expectedProtected = protection.CreateProtector("restore-test").Protect("restored-secret");
using (var scope = provider.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
    await db.Database.MigrateAsync(ct);
    db.Workspaces.Add(new Workspace { Id = FinanceService.SeedWorkspaceId, Name = "Teste", Kind = WorkspaceKind.Business, CreatedAt = DateTimeOffset.UtcNow });
    await db.SaveChangesAsync(ct);
}

var checks = 0;
void Check(bool value, string description)
{
    if (!value) throw new Exception("FAIL: " + description);
    Console.WriteLine("PASS: " + description); checks++;
}
async Task MustFail(Func<Task> action, string description)
{
    var rejected = false;
    try { await action(); } catch { rejected = true; }
    Check(rejected, description);
}

Guid userId;
string token;
using (var scope = provider.CreateScope())
{
    var auth = scope.ServiceProvider.GetRequiredService<AuthenticationService>();
    var user = await auth.RegisterAsync("Test User", "test@example.com", "InitialPassword123", ct); userId = user.Id;
    var result = await auth.RequestPasswordResetAsync(user.Email, ct); token = result.Token!;
    Check(token.Length == 64, "Development recovery supplies a test token");
    var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
    var queued = await db.EmailOutboxMessages.SingleAsync(ct);
    Check(!queued.ProtectedPayload.Contains(token) && !queued.ProtectedPayload.Contains(user.Email), "Queue encrypts token and recipient at rest");
    Check(await db.PasswordResetTokens.CountAsync(ct) == 1, "Token and email are committed together");
    await scope.ServiceProvider.GetRequiredService<WorkspaceAccessService>().InviteAsync(userId, FinanceService.SeedWorkspaceId, "invite@example.com", WorkspaceRole.Member, ct);
    Check(await db.EmailOutboxMessages.CountAsync(ct) == 2, "Invitation queues an email in its unit of work");
    await using (var transaction = await db.Database.BeginTransactionAsync(ct))
    {
        scope.ServiceProvider.GetRequiredService<EmailQueue>().Invitation("rolled-back@example.com", "Teste", DateTimeOffset.UtcNow.AddDays(7));
        await db.SaveChangesAsync(ct); await transaction.RollbackAsync(ct);
    }
    db.ChangeTracker.Clear();
    Check(await db.EmailOutboxMessages.CountAsync(ct) == 2, "Rollback does not leave orphaned email messages");
}

environment.EnvironmentName = "Production";
using (var scope = provider.CreateScope())
{
    var auth = scope.ServiceProvider.GetRequiredService<AuthenticationService>();
    var existing = await auth.RequestPasswordResetAsync("test@example.com", ct);
    var missing = await auth.RequestPasswordResetAsync("unknown@example.com", ct);
    Check(existing == missing && existing.Token == null && existing.ExpiresAt == null, "Production response does not expose token, expiry or account existence");
    var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
    var protector = protection.CreateProtector("NexoFinanceiro.EmailOutbox.v1");
    var newest = (await db.EmailOutboxMessages.ToListAsync(ct)).OrderByDescending(item => item.CreatedAt).First();
    token = System.Text.Json.JsonSerializer.Deserialize<EmailPayload>(protector.Unprotect(newest.ProtectedPayload))!.Body.Split("\n").Single(line => line.Length == 64);
}
environment.EnvironmentName = "Development";

var worker = new EmailOutboxWorker(provider.GetRequiredService<IServiceScopeFactory>(), protection, Options.Create(smtpOptions), provider.GetRequiredService<ILogger<EmailOutboxWorker>>());
sender.Fail = true;
await worker.ProcessOnceAsync(ct);
using (var scope = provider.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
    var rows = await db.EmailOutboxMessages.ToListAsync(ct);
    Check(rows.All(item => item.SentAt == null && item.Attempts == 1 && item.LastError == "DeliveryFailed"), "SMTP failure preserves jobs and schedules retry");
    foreach (var row in rows) row.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1);
    await db.SaveChangesAsync(ct);
}
sender.Fail = false;
await worker.ProcessOnceAsync(ct);
Check(sender.Messages.Any(item => item.Body.Contains(token) && item.Body.Contains("/#reset-password=")), "Recovery email contains working link and code");
Check(sender.Messages.Any(item => item.Recipient == "invite@example.com" && item.Body.Contains("Teste")), "Invitation goes to invited address with workspace context");
using (var scope = provider.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
    Check((await db.EmailOutboxMessages.ToListAsync(ct)).All(item => item.SentAt != null && item.ProtectedPayload == ""), "Successful delivery removes encrypted sensitive payload");
    var auth = scope.ServiceProvider.GetRequiredService<AuthenticationService>();
    await auth.ResetPasswordAsync(token, "UpdatedPassword123", ct);
    await MustFail(() => auth.ResetPasswordAsync(token, "UpdatedPassword123", ct), "Recovery token cannot be reused");
    Check((await auth.LoginAsync("test@example.com", "UpdatedPassword123", ct))?.User?.Id == userId, "Delivered token actually changes the login password");
    var queue = scope.ServiceProvider.GetRequiredService<EmailQueue>();
    queue.Invitation("expired@example.com", "Teste", DateTimeOffset.UtcNow.AddSeconds(-1));
    await db.SaveChangesAsync(ct);
}
var delivered = sender.Messages.Count;
await worker.ProcessOnceAsync(ct);
Check(sender.Messages.Count == delivered, "Expired messages are never sent");

// Exercise the actual SMTP adapter against a local protocol receiver, without external delivery.
var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
var port = ((IPEndPoint)listener.LocalEndpoint).Port;
var received = new TaskCompletionSource<string>();
var receiveTask = Task.Run(async () =>
{
    using var client = await listener.AcceptTcpClientAsync(); using var stream = client.GetStream();
    using var reader = new StreamReader(stream, Encoding.ASCII); using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" };
    await writer.WriteLineAsync("220 localhost SMTP");
    var body = new StringBuilder(); var data = false;
    while (await reader.ReadLineAsync() is { } line)
    {
        if (data)
        {
            if (line == ".") { data = false; received.TrySetResult(body.ToString()); await writer.WriteLineAsync("250 accepted"); }
            else body.AppendLine(line);
        }
        else if (line.StartsWith("EHLO") || line.StartsWith("HELO")) await writer.WriteLineAsync("250 localhost");
        else if (line.StartsWith("MAIL FROM") || line.StartsWith("RCPT TO")) await writer.WriteLineAsync("250 ok");
        else if (line == "DATA") { data = true; await writer.WriteLineAsync("354 send message"); }
        else if (line == "QUIT") { await writer.WriteLineAsync("221 bye"); break; }
        else await writer.WriteLineAsync("250 ok");
    }
});
using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
{
    var adapter = new SmtpEmailSender(Options.Create(new EmailOptions { Enabled = true, Host = "127.0.0.1", Port = port, StartTls = false, FromAddress = "nexo@example.com" }));
    await adapter.SendAsync(new EmailPayload("recipient@example.com", "Integration", "Local SMTP integration"), timeout.Token);
    var raw = await received.Task.WaitAsync(timeout.Token);
    Check(raw.Contains("recipient@example.com") && raw.Contains("Integration"), "Real SMTP adapter completes protocol delivery");
}
listener.Stop(); await receiveTask;
await MustFail(() => Task.Run(() => new EmailOptions { Enabled = true, PublicUrl = "https://nexo.example", Host = "smtp.example", StartTls = false, FromAddress = "nexo@example.com" }.Validate(false)), "Production refuses SMTP without TLS");

var attachmentBytes = "%PDF-1.7\nTest attachment"u8.ToArray();
var attachmentName = Guid.NewGuid().ToString("N") + ".pdf";
var attachmentPath = Path.Combine(root, "App_Data", "Attachments", FinanceService.SeedWorkspaceId.ToString("N"), attachmentName);
Directory.CreateDirectory(Path.GetDirectoryName(attachmentPath)!);
await File.WriteAllBytesAsync(attachmentPath, attachmentBytes, ct);
using (var scope = provider.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
    var accountId = Guid.NewGuid(); var transactionId = Guid.NewGuid();
    db.Accounts.Add(new FinancialAccount { Id = accountId, WorkspaceId = FinanceService.SeedWorkspaceId, Name = "Conta", Institution = "Teste", OpeningBalance = 125.50m });
    db.Transactions.Add(new FinancialTransaction { Id = transactionId, WorkspaceId = FinanceService.SeedWorkspaceId, AccountId = accountId,
        Description = "Receita", Category = "Serviços", Type = TransactionType.Income, Amount = 99.90m, OccurredOn = new DateOnly(2026, 10, 2), CreatedAt = DateTimeOffset.UtcNow });
    db.DocumentAttachments.Add(new DocumentAttachment { Id = Guid.NewGuid(), WorkspaceId = FinanceService.SeedWorkspaceId, EntityType = "Transaction",
        EntityId = transactionId, OriginalFileName = "comprovante.pdf", StoredFileName = attachmentName, ContentType = "application/pdf", Size = attachmentBytes.Length, UploadedByUserId = userId, UploadedAt = DateTimeOffset.UtcNow });
    await db.SaveChangesAsync(ct);
}
var backupOptions = new BackupOptions { RetentionCount = 2 };
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Database:File"] = "App_Data/finance.db" }).Build();
var backupGate = new BackupFileGate();
var backup = new BackupService(environment, config, Options.Create(backupOptions), backupGate, provider.GetRequiredService<ILogger<BackupService>>());
var file = await backup.CreateAsync(ct);
Check(backup.List().Count == 1 && backup.LastVerifiedAt != null, "Backup is published only after restore verification");
var verification = await backup.VerifyAsync(backup.GetArchivePath(file.Name), ct);
Check(verification.Attachments == 1 && verification.DatabaseIntegrity == "ok", "Verification checks database and attachment references");
var restored = Path.Combine(root, "Restored");
await backup.RestoreAsync(backup.GetArchivePath(file.Name), restored, ct);
var restoredOptions = new DbContextOptionsBuilder<FinanceDbContext>().UseSqlite($"Data Source={Path.Combine(restored, "App_Data", "finance.db")};Pooling=False").Options;
await using (var restoredDb = new FinanceDbContext(restoredOptions))
{
    Check((await restoredDb.Accounts.SingleAsync(ct)).OpeningBalance == 125.50m && (await restoredDb.Transactions.SingleAsync(ct)).Amount == 99.90m, "Restoration preserves financial amounts");
    Check(await restoredDb.Users.CountAsync(ct) == 1 && await restoredDb.WorkspaceInvitations.CountAsync(ct) == 1, "Restoration preserves users and invitations");
}
Check((await File.ReadAllBytesAsync(Path.Combine(restored, "App_Data", "Attachments", FinanceService.SeedWorkspaceId.ToString("N"), attachmentName), ct)).SequenceEqual(attachmentBytes), "Restoration preserves attachment bytes");
using (var keyProvider = new ServiceCollection().AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(restored, "App_Data", "DataProtection-Keys"))).SetApplicationName("NexoFinanceiro").Services.BuildServiceProvider())
    Check(keyProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("restore-test").Unprotect(expectedProtected) == "restored-secret", "Restored authentication keys decrypt original secrets");
Check(!File.Exists(Path.Combine(restored, "App_Data", "Backup-Key.key")), "Backup encryption key is excluded from archive");
await MustFail(() => backup.RestoreAsync(backup.GetArchivePath(file.Name), restored, ct), "Restoration refuses an existing destination");
var encryptedBytes = await File.ReadAllBytesAsync(backup.GetArchivePath(file.Name), ct); encryptedBytes[^1] ^= 1;
var corrupt = Path.Combine(root, "corrupt.nexobak"); await File.WriteAllBytesAsync(corrupt, encryptedBytes, ct);
await MustFail(() => backup.VerifyAsync(corrupt, ct), "Tampered backup fails authenticated decryption");
var wrongKeyOptions = new BackupOptions { EncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) };
var wrongKey = new BackupService(environment, config, Options.Create(wrongKeyOptions), backupGate, provider.GetRequiredService<ILogger<BackupService>>());
await MustFail(() => wrongKey.VerifyAsync(backup.GetArchivePath(file.Name), ct), "Wrong encryption key cannot verify a backup");
File.Delete(attachmentPath);
await MustFail(() => backup.CreateAsync(ct), "Missing referenced attachment prevents incomplete backup");
Check(backup.List().Count == 1, "Failed backup preserves previous archives");
await File.WriteAllBytesAsync(attachmentPath, attachmentBytes, ct);
await backup.CreateAsync(ct); await backup.CreateAsync(ct);
Check(backup.List().Count == 2, "Retention keeps configured number of verified archives");
Check(!Directory.EnumerateDirectories(backup.BackupDirectory).Any(), "Plaintext staging directories are cleaned after success and failure");
Console.WriteLine($"Completed: {checks} checks. Isolated data: {root}");
await FinancialChecks.RunAsync();

sealed class TestEnvironment(string root) : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "Financeiro.Api";
    public string EnvironmentName { get; set; } = "Development";
    public string ContentRootPath { get; set; } = root;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; } = root;
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}
sealed class RecordingSender : IEmailSender
{
    public bool Fail { get; set; }
    public List<EmailPayload> Messages { get; } = [];
    public Task SendAsync(EmailPayload payload, CancellationToken ct)
    {
        if (Fail) throw new InvalidOperationException("Simulated SMTP failure");
        Messages.Add(payload); return Task.CompletedTask;
    }
}

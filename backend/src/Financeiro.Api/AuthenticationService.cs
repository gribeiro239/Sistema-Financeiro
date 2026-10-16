using System.Net.Mail;
using System.Security.Cryptography;
using Financeiro.Application;
using Financeiro.Domain;
using Financeiro.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Financeiro.Api;

public sealed record AuthenticatedUser(Guid Id, string Name, string Email, string GlobalRole, bool IsTwoFactorEnabled, [property: System.Text.Json.Serialization.JsonIgnore] string SecurityStamp);
public sealed record PasswordRecoveryResult(string? Token, DateTimeOffset? ExpiresAt);
public sealed record LoginAttemptResult(AuthenticatedUser? User, bool RequiresTwoFactor, string? ChallengeToken, DateTimeOffset? ExpiresAt);
public sealed record TwoFactorSetupResult(string Secret, string OtpAuthUri);
public sealed record TwoFactorEnableResult(AuthenticatedUser User, IReadOnlyList<string> RecoveryCodes);

public sealed class AuthenticationService(FinanceDbContext database, IWebHostEnvironment environment, IDataProtectionProvider dataProtectionProvider, IHttpContextAccessor httpContextAccessor, EmailQueue emailQueue)
{
    private const int Iterations = 120_000;
    private readonly IDataProtector twoFactorProtector = dataProtectionProvider.CreateProtector("NexoFinanceiro.TwoFactor.v1");

    public async Task<AuthenticatedUser> RegisterAsync(string name, string email, string password, CancellationToken ct)
    {
        var cleanName = name.Trim(); var cleanEmail = NormalizeEmail(email);
        Validate(cleanName, cleanEmail, password);
        if (await database.Users.AnyAsync(user => user.Email == cleanEmail, ct)) throw new ArgumentException("Já existe uma conta com este e-mail.");

        await using var transaction = await database.Database.BeginTransactionAsync(ct);
        var isFirstUser = !await database.Users.AnyAsync(ct);
        var user = new ApplicationUser { Id = Guid.NewGuid(), Name = cleanName, Email = cleanEmail, PasswordHash = HashPassword(password), GlobalRole = isFirstUser ? GlobalRole.SuperAdmin : GlobalRole.User, CreatedAt = DateTimeOffset.UtcNow };
        database.Users.Add(user);

        Workspace workspace;
        if (isFirstUser)
        {
            workspace = await database.Workspaces.FirstAsync(item => item.Id == FinanceService.SeedWorkspaceId, ct);
        }
        else
        {
            workspace = new Workspace { Id = Guid.NewGuid(), Name = $"Espaço de {cleanName.Split(' ')[0]}", Kind = WorkspaceKind.Personal, CreatedAt = DateTimeOffset.UtcNow };
            database.Workspaces.Add(workspace);
        }
        database.WorkspaceMembers.Add(new WorkspaceMember { WorkspaceId = workspace.Id, UserId = user.Id, Role = WorkspaceRole.Owner, JoinedAt = DateTimeOffset.UtcNow });
        await database.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return ToView(user);
    }

    public async Task<LoginAttemptResult?> LoginAsync(string email, string password, CancellationToken ct)
    {
        var cleanEmail = NormalizeEmail(email);
        var user = await database.Users.FirstOrDefaultAsync(item => item.Email == cleanEmail, ct);
        if (user is null || !user.IsActive || !VerifyPassword(password, user.PasswordHash)) return null;
        if (!user.IsTwoFactorEnabled) return new LoginAttemptResult(ToView(user), false, null, null);
        var now = DateTimeOffset.UtcNow;
        if (user.TwoFactorLockedUntil > now) throw new InvalidOperationException($"Segundo fator bloqueado até {user.TwoFactorLockedUntil:O}.");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiresAt = now.AddMinutes(5);
        database.TwoFactorLoginChallenges.Add(new TwoFactorLoginChallenge { Id = Guid.NewGuid(), UserId = user.Id, TokenHash = HashToken(token), ExpiresAt = expiresAt, CreatedAt = now });
        await database.SaveChangesAsync(ct);
        return new LoginAttemptResult(null, true, token, expiresAt);
    }

    public async Task<AuthenticatedUser?> GetAsync(Guid userId, CancellationToken ct)
    {
        var user = await database.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == userId, ct);
        return user is null ? null : ToView(user);
    }

    public async Task<AuthenticatedUser> UpdateProfileAsync(Guid userId, string name, string email, CancellationToken ct)
    {
        var cleanName = name.Trim(); var cleanEmail = NormalizeEmail(email);
        ValidateProfile(cleanName, cleanEmail);
        if (await database.Users.AnyAsync(user => user.Id != userId && user.Email == cleanEmail, ct))
            throw new ArgumentException("Já existe uma conta com este e-mail.");
        var user = await database.Users.FirstOrDefaultAsync(item => item.Id == userId, ct)
            ?? throw new InvalidOperationException("Usuário não encontrado.");
        user.Name = cleanName; user.Email = cleanEmail;
        await database.SaveChangesAsync(ct);
        return ToView(user);
    }

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct)
    {
        var user = await database.Users.FirstOrDefaultAsync(item => item.Id == userId, ct)
            ?? throw new InvalidOperationException("Usuário não encontrado.");
        if (!VerifyPassword(currentPassword, user.PasswordHash)) throw new ArgumentException("A senha atual está incorreta.");
        if (newPassword.Length is < 8 or > 128) throw new ArgumentException("A nova senha deve ter entre 8 e 128 caracteres.");
        user.PasswordHash = HashPassword(newPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await database.SaveChangesAsync(ct);
    }

    public Task<TwoFactorSetupResult> BeginTwoFactorSetupAsync(Guid userId, CancellationToken ct)
    {
        var secret = Base32Encode(RandomNumberGenerator.GetBytes(20));
        return GetSetupAsync();

        async Task<TwoFactorSetupResult> GetSetupAsync()
        {
            var user = await database.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == userId, ct) ?? throw new KeyNotFoundException("Usuário não encontrado.");
            var issuer = Uri.EscapeDataString("Nexo Financeiro"); var account = Uri.EscapeDataString(user.Email);
            return new TwoFactorSetupResult(secret, $"otpauth://totp/{issuer}:{account}?secret={secret}&issuer={issuer}&digits=6&period=30");
        }
    }

    public async Task<TwoFactorEnableResult> EnableTwoFactorAsync(Guid userId, string secret, string code, CancellationToken ct)
    {
        var user = await database.Users.FirstOrDefaultAsync(item => item.Id == userId, ct) ?? throw new KeyNotFoundException("Usuário não encontrado.");
        var cleanSecret = secret.Trim().Replace(" ", "").ToUpperInvariant();
        byte[] secretBytes; try { secretBytes = Base32Decode(cleanSecret); } catch { throw new ArgumentException("Chave de autenticação inválida."); }
        if (!VerifyTotp(secretBytes, code)) throw new ArgumentException("Código do autenticador inválido.");
        var previous = await database.TwoFactorRecoveryCodes.Where(item => item.UserId == userId).ToListAsync(ct); database.TwoFactorRecoveryCodes.RemoveRange(previous);
        var recoveryCodes = Enumerable.Range(0, 10).Select(_ => Convert.ToHexString(RandomNumberGenerator.GetBytes(5))).ToList();
        database.TwoFactorRecoveryCodes.AddRange(recoveryCodes.Select(value => new TwoFactorRecoveryCode { Id = Guid.NewGuid(), UserId = userId, CodeHash = HashToken(value), CreatedAt = DateTimeOffset.UtcNow }));
        user.TwoFactorSecret = twoFactorProtector.Protect(cleanSecret); user.IsTwoFactorEnabled = true; user.FailedTwoFactorAttempts = 0; user.TwoFactorLockedUntil = null; user.SecurityStamp = Guid.NewGuid().ToString("N");
        AddSecurityAudit(user.Id, "TwoFactorEnabled", "Autenticação em dois fatores ativada.");
        await database.SaveChangesAsync(ct);
        return new TwoFactorEnableResult(ToView(user), recoveryCodes);
    }

    public async Task<AuthenticatedUser> CompleteTwoFactorLoginAsync(string challengeToken, string code, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow; var hash = HashToken(challengeToken.Trim());
        var challenge = await database.TwoFactorLoginChallenges.FirstOrDefaultAsync(item => item.TokenHash == hash && item.UsedAt == null, ct) ?? throw new ArgumentException("Desafio de autenticação inválido ou expirado.");
        if (challenge.ExpiresAt <= now) { challenge.UsedAt = now; await database.SaveChangesAsync(ct); throw new ArgumentException("Desafio de autenticação inválido ou expirado."); }
        var user = await database.Users.FirstOrDefaultAsync(item => item.Id == challenge.UserId, ct) ?? throw new ArgumentException("Usuário não encontrado.");
        if (!user.IsActive || !user.IsTwoFactorEnabled || string.IsNullOrWhiteSpace(user.TwoFactorSecret)) throw new ArgumentException("Autenticação em dois fatores indisponível.");
        if (user.TwoFactorLockedUntil > now) throw new InvalidOperationException($"Segundo fator bloqueado até {user.TwoFactorLockedUntil:O}.");
        var valid = VerifySecondFactor(user, code, out var recoveryHash);
        if (!valid)
        {
            user.FailedTwoFactorAttempts++;
            if (user.FailedTwoFactorAttempts >= 5) { user.TwoFactorLockedUntil = now.AddMinutes(15); user.FailedTwoFactorAttempts = 0; challenge.UsedAt = now; }
            await database.SaveChangesAsync(ct); throw new ArgumentException("Código de autenticação inválido.");
        }
        if (recoveryHash is not null)
        {
            var recovery = await database.TwoFactorRecoveryCodes.FirstAsync(item => item.UserId == user.Id && item.CodeHash == recoveryHash && item.UsedAt == null, ct); recovery.UsedAt = now;
            AddSecurityAudit(user.Id, "RecoveryCodeUsed", "Código de recuperação utilizado no login.");
        }
        user.FailedTwoFactorAttempts = 0; user.TwoFactorLockedUntil = null; challenge.UsedAt = now;
        await database.SaveChangesAsync(ct); return ToView(user);
    }

    public async Task<AuthenticatedUser> DisableTwoFactorAsync(Guid userId, string password, string code, CancellationToken ct)
    {
        var user = await database.Users.FirstOrDefaultAsync(item => item.Id == userId, ct) ?? throw new KeyNotFoundException("Usuário não encontrado.");
        if (user.GlobalRole is GlobalRole.SuperAdmin or GlobalRole.Support or GlobalRole.Developer) throw new InvalidOperationException("O segundo fator é obrigatório para contas privilegiadas. Altere o papel global antes de removê-lo.");
        if (!VerifyPassword(password, user.PasswordHash)) throw new ArgumentException("A senha atual está incorreta.");
        if (!user.IsTwoFactorEnabled || !VerifySecondFactor(user, code, out var recoveryHash)) throw new ArgumentException("Código de autenticação inválido.");
        var recovery = await database.TwoFactorRecoveryCodes.Where(item => item.UserId == userId).ToListAsync(ct); database.TwoFactorRecoveryCodes.RemoveRange(recovery);
        user.TwoFactorSecret = null; user.IsTwoFactorEnabled = false; user.FailedTwoFactorAttempts = 0; user.TwoFactorLockedUntil = null; user.SecurityStamp = Guid.NewGuid().ToString("N");
        AddSecurityAudit(user.Id, "TwoFactorDisabled", "Autenticação em dois fatores removida.");
        await database.SaveChangesAsync(ct); return ToView(user);
    }

    public async Task<PasswordRecoveryResult> RequestPasswordResetAsync(string email, CancellationToken ct)
    {
        string cleanEmail;
        try { cleanEmail = NormalizeEmail(email); }
        catch (ArgumentException) { return new PasswordRecoveryResult(null, null); }
        var user = await database.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Email == cleanEmail && item.IsActive, ct);
        if (user is null) return new PasswordRecoveryResult(null, null);
        return await CreatePasswordResetAsync(user, ct, exposeToken: false);
    }

    public async Task ResetPasswordAsync(string token, string newPassword, CancellationToken ct)
    {
        if (newPassword.Length is < 8 or > 128) throw new ArgumentException("A nova senha deve ter entre 8 e 128 caracteres.");
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Código de recuperação inválido ou expirado.");
        var hash = HashToken(token.Trim());
        var now = DateTimeOffset.UtcNow;
        var reset = await database.PasswordResetTokens.FirstOrDefaultAsync(item => item.TokenHash == hash && item.UsedAt == null, ct);
        if (reset is null || reset.ExpiresAt <= now) throw new ArgumentException("Código de recuperação inválido ou expirado.");
        var user = await database.Users.FirstOrDefaultAsync(item => item.Id == reset.UserId, ct)
            ?? throw new ArgumentException("Código de recuperação inválido ou expirado.");
        user.PasswordHash = HashPassword(newPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        var activeTokens = await database.PasswordResetTokens.Where(item => item.UserId == user.Id && item.UsedAt == null).ToListAsync(ct);
        foreach (var item in activeTokens) item.UsedAt = now;
        await database.SaveChangesAsync(ct);
    }

    private static void Validate(string name, string email, string password)
    {
        ValidateProfile(name, email);
        if (password.Length is < 8 or > 128) throw new ArgumentException("A senha deve ter entre 8 e 128 caracteres.");
    }

    private static void ValidateProfile(string name, string email)
    {
        if (name.Length is < 2 or > 80) throw new ArgumentException("O nome deve ter entre 2 e 80 caracteres.");
        try { _ = new MailAddress(email); } catch { throw new ArgumentException("Informe um e-mail válido."); }
        if (email.Length > 254) throw new ArgumentException("O e-mail é muito longo.");
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    public async Task<PasswordRecoveryResult> RequestPasswordResetForUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await database.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == userId, ct)
            ?? throw new KeyNotFoundException("Usuário não encontrado.");
        return await CreatePasswordResetAsync(user, ct, exposeToken: true);
    }

    private async Task<PasswordRecoveryResult> CreatePasswordResetAsync(ApplicationUser user, CancellationToken ct, bool exposeToken)
    {
        var now = DateTimeOffset.UtcNow;
        var previous = await database.PasswordResetTokens.Where(item => item.UserId == user.Id && item.UsedAt == null).ToListAsync(ct);
        foreach (var item in previous) item.UsedAt = now;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiresAt = now.AddMinutes(20);
        database.PasswordResetTokens.Add(new PasswordResetToken { Id = Guid.NewGuid(), UserId = user.Id, TokenHash = HashToken(token), ExpiresAt = expiresAt, CreatedAt = now });
        emailQueue.PasswordReset(user.Email, token, expiresAt);
        await database.SaveChangesAsync(ct);
        return new PasswordRecoveryResult(exposeToken || environment.IsDevelopment() ? token : null,
            exposeToken || environment.IsDevelopment() ? expiresAt : null);
    }

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }
    private static bool VerifyPassword(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations)) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[1]); var expected = Convert.FromBase64String(parts[2]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }
    private bool VerifySecondFactor(ApplicationUser user, string code, out string? recoveryHash)
    {
        recoveryHash = null; var normalized = code.Trim().Replace("-", "").Replace(" ", "").ToUpperInvariant();
        if (normalized.Length == 6 && normalized.All(char.IsDigit))
        {
            try { return VerifyTotp(Base32Decode(twoFactorProtector.Unprotect(user.TwoFactorSecret!)), normalized); } catch { return false; }
        }
        var hash = HashToken(normalized);
        var validRecovery = database.TwoFactorRecoveryCodes.Any(item => item.UserId == user.Id && item.CodeHash == hash && item.UsedAt == null);
        if (validRecovery) recoveryHash = hash; return validRecovery;
    }

    private static bool VerifyTotp(byte[] secret, string code)
    {
        var normalized = code.Trim().Replace(" ", ""); if (normalized.Length != 6 || !int.TryParse(normalized, out var expected)) return false;
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        for (var offset = -1; offset <= 1; offset++) if (GenerateTotp(secret, counter + offset) == expected) return true;
        return false;
    }

    private static int GenerateTotp(byte[] secret, long counter)
    {
        Span<byte> counterBytes = stackalloc byte[8]; System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);
        using var hmac = new HMACSHA1(secret); var hash = hmac.ComputeHash(counterBytes.ToArray()); var offset = hash[^1] & 0x0f;
        var value = ((hash[offset] & 0x7f) << 24) | ((hash[offset + 1] & 0xff) << 16) | ((hash[offset + 2] & 0xff) << 8) | (hash[offset + 3] & 0xff);
        return value % 1_000_000;
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"; var output = new System.Text.StringBuilder(); var buffer = 0; var bits = 0;
        foreach (var value in data) { buffer = (buffer << 8) | value; bits += 8; while (bits >= 5) { bits -= 5; output.Append(alphabet[(buffer >> bits) & 31]); } }
        if (bits > 0) output.Append(alphabet[(buffer << (5 - bits)) & 31]); return output.ToString();
    }

    private static byte[] Base32Decode(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"; var bytes = new List<byte>(); var buffer = 0; var bits = 0;
        foreach (var character in value.TrimEnd('=')) { var index = alphabet.IndexOf(char.ToUpperInvariant(character)); if (index < 0) throw new FormatException(); buffer = (buffer << 5) | index; bits += 5; if (bits >= 8) { bits -= 8; bytes.Add((byte)((buffer >> bits) & 255)); } }
        return bytes.ToArray();
    }

    private void AddSecurityAudit(Guid userId, string action, string description)
        => database.PlatformAuditEntries.Add(new PlatformAuditEntry { Id = Guid.NewGuid(), ActorUserId = userId, Action = action, EntityType = "UserSecurity", EntityId = userId, Description = description, IpAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(), OccurredAt = DateTimeOffset.UtcNow });

    private static AuthenticatedUser ToView(ApplicationUser user) => new(user.Id, user.Name, user.Email, user.GlobalRole.ToString(), user.IsTwoFactorEnabled, user.SecurityStamp);
}

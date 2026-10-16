using System.Reflection;
using System.Text.Json;
using Financeiro.Domain;
using Financeiro.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Financeiro.Api;

public sealed record PlatformOverview(int UserCount, int ActiveUserCount, int WorkspaceCount, int SuperAdminCount, int DeveloperCount, int AuditEventCount);
public sealed record PlatformUserView(Guid Id, string Name, string Email, string GlobalRole, bool IsActive, int WorkspaceCount, DateTimeOffset CreatedAt);
public sealed record PlatformWorkspaceView(Guid Id, string Name, string Kind, int MemberCount, DateTimeOffset CreatedAt);
public sealed record PlatformAuditView(Guid Id, Guid ActorUserId, string ActorName, string Action, string EntityType, Guid? EntityId, string Description, string? Details, string? IpAddress, DateTimeOffset OccurredAt);
public sealed record PlatformDiagnosticView(string Status, string Environment, string Version, string DatabaseProvider, int AppliedMigrations, int PendingMigrations, DateTimeOffset ServerTimeUtc);
public sealed record PlatformSettingView(string Key, string Value, Guid UpdatedByUserId, DateTimeOffset UpdatedAt);

public sealed class PlatformAdministrationService(FinanceDbContext database, AuthenticationService authentication, IWebHostEnvironment environment, IHttpContextAccessor httpContextAccessor)
{
    public static readonly string[] AllowedSettingKeys = ["MaintenanceNotice", "BankImportEnabled", "ExperimentalReportsEnabled"];

    public async Task<PlatformOverview> GetOverviewAsync(CancellationToken ct)
        => new(await database.Users.CountAsync(ct), await database.Users.CountAsync(item => item.IsActive, ct), await database.Workspaces.CountAsync(ct), await database.Users.CountAsync(item => item.GlobalRole == GlobalRole.SuperAdmin, ct), await database.Users.CountAsync(item => item.GlobalRole == GlobalRole.Developer, ct), await database.PlatformAuditEntries.CountAsync(ct));

    public async Task<IReadOnlyList<PlatformUserView>> GetUsersAsync(CancellationToken ct)
    {
        var users = await database.Users.AsNoTracking().OrderBy(item => item.Name).ToListAsync(ct);
        var memberships = await database.WorkspaceMembers.AsNoTracking().GroupBy(item => item.UserId).Select(group => new { UserId = group.Key, Count = group.Count() }).ToDictionaryAsync(item => item.UserId, item => item.Count, ct);
        return users.Select(item => new PlatformUserView(item.Id, item.Name, item.Email, item.GlobalRole.ToString(), item.IsActive, memberships.GetValueOrDefault(item.Id), item.CreatedAt)).ToList();
    }

    public async Task<IReadOnlyList<PlatformWorkspaceView>> GetWorkspacesAsync(CancellationToken ct)
    {
        var workspaces = await database.Workspaces.AsNoTracking().OrderBy(item => item.Name).ToListAsync(ct);
        var members = await database.WorkspaceMembers.AsNoTracking().GroupBy(item => item.WorkspaceId).Select(group => new { WorkspaceId = group.Key, Count = group.Count() }).ToDictionaryAsync(item => item.WorkspaceId, item => item.Count, ct);
        return workspaces.Select(item => new PlatformWorkspaceView(item.Id, item.Name, item.Kind.ToString(), members.GetValueOrDefault(item.Id), item.CreatedAt)).ToList();
    }

    public async Task<PlatformUserView> SetRoleAsync(Guid actorId, Guid userId, GlobalRole role, CancellationToken ct)
    {
        if (actorId == userId) throw new InvalidOperationException("Você não pode alterar seu próprio papel global.");
        var user = await database.Users.FirstOrDefaultAsync(item => item.Id == userId, ct) ?? throw new KeyNotFoundException("Usuário não encontrado.");
        if (user.GlobalRole == GlobalRole.SuperAdmin && role != GlobalRole.SuperAdmin && await database.Users.CountAsync(item => item.GlobalRole == GlobalRole.SuperAdmin && item.IsActive, ct) <= 1)
            throw new InvalidOperationException("A plataforma precisa manter ao menos um superadministrador ativo.");
        var previousRole = user.GlobalRole; user.GlobalRole = role; user.SecurityStamp = Guid.NewGuid().ToString("N");
        await AddAuditAsync(actorId, "RoleChanged", "User", user.Id, $"Papel global alterado para {role}: {user.Email}", new { PreviousRole = previousRole, NewRole = role }, ct);
        await database.SaveChangesAsync(ct);
        return (await GetUsersAsync(ct)).First(item => item.Id == user.Id);
    }

    public async Task<PlatformUserView> SetActiveAsync(Guid actorId, Guid userId, bool active, CancellationToken ct)
    {
        if (actorId == userId) throw new InvalidOperationException("Você não pode bloquear sua própria conta.");
        var user = await database.Users.FirstOrDefaultAsync(item => item.Id == userId, ct) ?? throw new KeyNotFoundException("Usuário não encontrado.");
        if (!active && user.GlobalRole == GlobalRole.SuperAdmin && await database.Users.CountAsync(item => item.GlobalRole == GlobalRole.SuperAdmin && item.IsActive, ct) <= 1)
            throw new InvalidOperationException("A plataforma precisa manter ao menos um superadministrador ativo.");
        user.IsActive = active; user.SecurityStamp = Guid.NewGuid().ToString("N");
        await AddAuditAsync(actorId, active ? "Activated" : "Blocked", "User", user.Id, $"Usuário {(active ? "reativado" : "bloqueado")}: {user.Email}", null, ct);
        await database.SaveChangesAsync(ct);
        return (await GetUsersAsync(ct)).First(item => item.Id == user.Id);
    }

    public async Task<PasswordRecoveryResult> IssuePasswordResetAsync(Guid actorId, Guid userId, CancellationToken ct)
    {
        var actor = await database.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == actorId, ct) ?? throw new KeyNotFoundException("Administrador não encontrado.");
        var target = await database.Users.FirstOrDefaultAsync(item => item.Id == userId, ct) ?? throw new KeyNotFoundException("Usuário não encontrado.");
        if (!target.IsActive) throw new InvalidOperationException("Reative o usuário antes de redefinir sua senha.");
        if (actor.GlobalRole == GlobalRole.Support && target.GlobalRole != GlobalRole.User) throw new InvalidOperationException("O suporte não pode redefinir a senha de uma conta privilegiada.");
        var result = await authentication.RequestPasswordResetForUserAsync(userId, ct);
        target.SecurityStamp = Guid.NewGuid().ToString("N");
        await AddAuditAsync(actorId, "PasswordResetIssued", "User", target.Id, $"Redefinição de senha iniciada: {target.Email}", new { result.ExpiresAt }, ct);
        await database.SaveChangesAsync(ct);
        return result;
    }

    public async Task<IReadOnlyList<PlatformAuditView>> GetAuditAsync(CancellationToken ct)
    {
        var entries = await database.PlatformAuditEntries.AsNoTracking().ToListAsync(ct);
        var names = await database.Users.AsNoTracking().ToDictionaryAsync(item => item.Id, item => item.Name, ct);
        return entries.OrderByDescending(item => item.OccurredAt).Take(500).Select(item => new PlatformAuditView(item.Id, item.ActorUserId, names.GetValueOrDefault(item.ActorUserId, "Usuário removido"), item.Action, item.EntityType, item.EntityId, item.Description, item.Details, item.IpAddress, item.OccurredAt)).ToList();
    }

    public async Task<PlatformDiagnosticView> GetDiagnosticsAsync(CancellationToken ct)
    {
        var applied = (await database.Database.GetAppliedMigrationsAsync(ct)).Count();
        var pending = (await database.Database.GetPendingMigrationsAsync(ct)).Count();
        return new PlatformDiagnosticView("Healthy", environment.EnvironmentName, Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown", database.Database.ProviderName ?? "unknown", applied, pending, DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<PlatformSettingView>> GetSettingsAsync(CancellationToken ct)
    {
        var settings = await database.PlatformSettings.AsNoTracking().ToDictionaryAsync(item => item.Key, ct);
        return AllowedSettingKeys.Select(key => settings.TryGetValue(key, out var item) ? new PlatformSettingView(item.Key, item.Value, item.UpdatedByUserId, item.UpdatedAt) : new PlatformSettingView(key, DefaultValue(key), Guid.Empty, DateTimeOffset.MinValue)).ToList();
    }

    public async Task<PlatformSettingView> SaveSettingAsync(Guid actorId, string key, string value, CancellationToken ct)
    {
        if (!AllowedSettingKeys.Contains(key, StringComparer.Ordinal)) throw new ArgumentException("Configuração técnica não permitida.");
        var cleanValue = value.Trim(); if (cleanValue.Length > 500) throw new ArgumentException("O valor deve ter até 500 caracteres.");
        var setting = await database.PlatformSettings.FirstOrDefaultAsync(item => item.Key == key, ct);
        var oldValue = setting?.Value;
        if (setting is null) { setting = new PlatformSetting { Key = key, Value = cleanValue, UpdatedByUserId = actorId, UpdatedAt = DateTimeOffset.UtcNow }; database.PlatformSettings.Add(setting); }
        else { setting.Value = cleanValue; setting.UpdatedByUserId = actorId; setting.UpdatedAt = DateTimeOffset.UtcNow; }
        await AddAuditAsync(actorId, "SettingChanged", "PlatformSetting", null, $"Configuração técnica alterada: {key}", new { PreviousValue = oldValue, NewValue = cleanValue }, ct);
        await database.SaveChangesAsync(ct);
        return new PlatformSettingView(setting.Key, setting.Value, setting.UpdatedByUserId, setting.UpdatedAt);
    }

    private void AddAudit(Guid actorId, string action, string entityType, Guid? entityId, string description, object? details)
        => database.PlatformAuditEntries.Add(new PlatformAuditEntry { Id = Guid.NewGuid(), ActorUserId = actorId, Action = action, EntityType = entityType, EntityId = entityId, Description = description, Details = details is null ? null : JsonSerializer.Serialize(details), IpAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(), OccurredAt = DateTimeOffset.UtcNow });

    private Task AddAuditAsync(Guid actorId, string action, string entityType, Guid? entityId, string description, object? details, CancellationToken ct)
    { AddAudit(actorId, action, entityType, entityId, description, details); return Task.CompletedTask; }

    private static string DefaultValue(string key) => key switch { "BankImportEnabled" => "true", "ExperimentalReportsEnabled" => "false", _ => "" };
}

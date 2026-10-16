namespace Financeiro.Domain;

public enum GlobalRole
{
    User,
    Support,
    Developer,
    SuperAdmin
}

public sealed class ApplicationUser
{
    public Guid Id { get; init; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public GlobalRole GlobalRole { get; set; } = GlobalRole.User;
    public bool IsActive { get; set; } = true;
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public string? TwoFactorSecret { get; set; }
    public bool IsTwoFactorEnabled { get; set; }
    public int FailedTwoFactorAttempts { get; set; }
    public DateTimeOffset? TwoFactorLockedUntil { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class PlatformAuditEntry
{
    public Guid Id { get; init; }
    public Guid ActorUserId { get; init; }
    public required string Action { get; init; }
    public required string EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public required string Description { get; init; }
    public string? Details { get; init; }
    public string? IpAddress { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}

public sealed class PlatformSetting
{
    public required string Key { get; init; }
    public required string Value { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class TwoFactorRecoveryCode
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public required string CodeHash { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UsedAt { get; set; }
}

public sealed class TwoFactorLoginChallenge
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public required string TokenHash { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
}

public enum WorkspaceRole
{
    Owner,
    Administrator,
    Member
}

public sealed class WorkspaceMember
{
    public Guid WorkspaceId { get; init; }
    public Guid UserId { get; init; }
    public WorkspaceRole Role { get; set; }
    public DateTimeOffset JoinedAt { get; init; }
}

public enum WorkspaceInvitationStatus { Pending, Accepted, Revoked, Expired }

public sealed class WorkspaceInvitation
{
    public Guid Id { get; init; }
    public Guid WorkspaceId { get; init; }
    public Guid InvitedByUserId { get; init; }
    public required string Email { get; init; }
    public WorkspaceRole Role { get; init; }
    public required string TokenHash { get; init; }
    public WorkspaceInvitationStatus Status { get; set; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? AcceptedAt { get; set; }
}

public sealed class PasswordResetToken
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public required string TokenHash { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
}

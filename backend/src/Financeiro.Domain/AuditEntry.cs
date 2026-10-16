namespace Financeiro.Domain;

public sealed class AuditEntry
{
    public Guid Id { get; init; }
    public Guid WorkspaceId { get; init; }
    public Guid UserId { get; init; }
    public required string Action { get; init; }
    public required string EntityType { get; init; }
    public Guid EntityId { get; init; }
    public required string Description { get; init; }
    public string? Details { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}

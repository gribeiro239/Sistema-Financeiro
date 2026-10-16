namespace Financeiro.Domain;

public sealed class ReconciliationHistoryEntry
{
    public Guid Id { get; init; }
    public Guid WorkspaceId { get; init; }
    public Guid BankStatementEntryId { get; init; }
    public Guid AccountId { get; init; }
    public Guid UserId { get; init; }
    public required string Action { get; init; }
    public Guid? TransactionId { get; init; }
    public bool TransactionWasCreated { get; init; }
    public bool TransactionRemoved { get; init; }
    public required string Description { get; init; }
    public string? Category { get; init; }
    public string? CostCenterName { get; init; }
    public string? RuleName { get; init; }
    public string? Details { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}

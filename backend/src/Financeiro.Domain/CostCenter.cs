namespace Financeiro.Domain;

public sealed class CostCenter
{
    public Guid Id { get; init; }
    public Guid WorkspaceId { get; init; }
    public required string Name { get; init; }
    public required string Code { get; init; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; init; }

    public void SetActive(bool active) => IsActive = active;
}

public sealed class TransactionAllocation
{
    public Guid Id { get; init; }
    public Guid WorkspaceId { get; init; }
    public Guid TransactionId { get; init; }
    public Guid CostCenterId { get; init; }
    public decimal Amount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

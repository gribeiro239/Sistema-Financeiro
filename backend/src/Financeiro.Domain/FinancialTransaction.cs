namespace Financeiro.Domain;

public enum TransactionType
{
    Income,
    Expense
}

public sealed class FinancialTransaction
{
    public Guid Id { get; init; }
    public Guid WorkspaceId { get; init; }
    public Guid AccountId { get; set; }
    public required string Description { get; set; }
    public required string Category { get; set; }
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public DateOnly OccurredOn { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public Guid? TransferId { get; init; }
    public Guid? CommitmentId { get; init; }
    public Guid? CounterpartyAccountId { get; set; }
    public bool IsVoided { get; private set; }
    public DateTimeOffset? VoidedAt { get; private set; }

    public decimal SignedAmount => Type == TransactionType.Income ? Amount : -Amount;
    public bool IsTransfer => TransferId.HasValue;

    public void Void(DateTimeOffset when)
    {
        if (IsVoided) throw new InvalidOperationException("Este lançamento já foi estornado.");
        IsVoided = true;
        VoidedAt = when;
    }

    public void Update(Guid accountId, string description, string category, TransactionType type, decimal amount, DateOnly occurredOn, Guid? counterpartyAccountId = null)
    {
        AccountId = accountId;
        Description = description;
        Category = category;
        Type = type;
        Amount = amount;
        OccurredOn = occurredOn;
        CounterpartyAccountId = counterpartyAccountId;
    }
}

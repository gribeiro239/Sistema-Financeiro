namespace Financeiro.Domain;

public enum ReconciliationStatus
{
    Pending,
    Reconciled,
    Ignored
}

public sealed class BankStatementEntry
{
    public Guid Id { get; init; }
    public Guid WorkspaceId { get; init; }
    public Guid AccountId { get; init; }
    public required string ImportKey { get; init; }
    public string? ExternalId { get; init; }
    public required string Description { get; init; }
    public TransactionType Type { get; init; }
    public decimal Amount { get; init; }
    public DateOnly OccurredOn { get; init; }
    public ReconciliationStatus Status { get; private set; }
    public Guid? TransactionId { get; private set; }
    public DateTimeOffset ImportedAt { get; init; }
    public DateTimeOffset? ReconciledAt { get; private set; }

    public decimal SignedAmount => Type == TransactionType.Income ? Amount : -Amount;

    public void Reconcile(Guid transactionId)
    {
        if (Status == ReconciliationStatus.Reconciled) throw new InvalidOperationException("Esta movimentação bancária já foi conciliada.");
        TransactionId = transactionId;
        Status = ReconciliationStatus.Reconciled;
        ReconciledAt = DateTimeOffset.UtcNow;
    }

    public void Ignore()
    {
        if (Status == ReconciliationStatus.Reconciled) throw new InvalidOperationException("Desfaça a conciliação antes de ignorar.");
        Status = ReconciliationStatus.Ignored;
    }

    public void Reopen()
    {
        Status = ReconciliationStatus.Pending;
        TransactionId = null;
        ReconciledAt = null;
    }
}

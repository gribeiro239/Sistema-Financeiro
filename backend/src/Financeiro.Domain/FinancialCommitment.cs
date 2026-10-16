namespace Financeiro.Domain;

public enum CommitmentKind { Payable, Receivable }
public enum CommitmentStatus { Open, PartiallySettled, Settled, Cancelled }
public enum RecurrenceFrequency { Weekly, Monthly, Yearly }

public sealed class FinancialCommitment
{
    public Guid Id { get; init; }
    public Guid WorkspaceId { get; init; }
    public Guid SeriesId { get; init; }
    public Guid? ContactId { get; set; }
    public required string Description { get; set; }
    public required string Contact { get; set; }
    public required string Category { get; set; }
    public CommitmentKind Kind { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal SettledAmount { get; private set; }
    public DateOnly DueDate { get; set; }
    public DateOnly CompetenceDate { get; set; }
    public int InstallmentNumber { get; init; } = 1;
    public int InstallmentCount { get; init; } = 1;
    public RecurrenceFrequency? RecurrenceFrequency { get; init; }
    public int RecurrenceNumber { get; init; } = 1;
    public int RecurrenceCount { get; init; } = 1;
    public CommitmentStatus Status { get; private set; } = CommitmentStatus.Open;
    public DateTimeOffset CreatedAt { get; init; }
    public decimal RemainingAmount => TotalAmount - SettledAmount;

    public void Settle(decimal amount)
    {
        if (Status is CommitmentStatus.Settled or CommitmentStatus.Cancelled) throw new InvalidOperationException("Este compromisso não aceita novas baixas.");
        amount = FinancialMoney.Positive(amount);
        if (amount > RemainingAmount) throw new ArgumentException("A baixa não pode superar o saldo restante.");
        SettledAmount += amount;
        Status = SettledAmount == TotalAmount ? CommitmentStatus.Settled : CommitmentStatus.PartiallySettled;
    }

    public void Cancel()
    {
        if (SettledAmount > 0) throw new InvalidOperationException("Um compromisso com baixas não pode ser cancelado.");
        Status = CommitmentStatus.Cancelled;
    }

    public void ReverseSettlement(decimal amount)
    {
        amount = FinancialMoney.Positive(amount);
        if (amount > SettledAmount || Status == CommitmentStatus.Cancelled)
            throw new InvalidOperationException("A baixa não pode ser estornada neste compromisso.");
        SettledAmount -= amount;
        Status = SettledAmount == 0 ? CommitmentStatus.Open : CommitmentStatus.PartiallySettled;
    }

    public void Update(string description, string contact, string category, CommitmentKind kind, decimal totalAmount, DateOnly dueDate, DateOnly competenceDate)
    {
        totalAmount = FinancialMoney.Positive(totalAmount);
        if (Status is CommitmentStatus.Settled or CommitmentStatus.Cancelled) throw new InvalidOperationException("Somente compromissos em aberto podem ser editados.");
        if (totalAmount < SettledAmount) throw new ArgumentException("O valor total não pode ser menor que o valor já baixado.");
        if (SettledAmount > 0 && kind != Kind) throw new InvalidOperationException("O tipo não pode ser alterado após uma baixa parcial.");
        Description = description;
        Contact = contact;
        Category = category;
        Kind = kind;
        TotalAmount = totalAmount;
        DueDate = dueDate;
        CompetenceDate = competenceDate;
        Status = SettledAmount == TotalAmount ? CommitmentStatus.Settled : SettledAmount == 0 ? CommitmentStatus.Open : CommitmentStatus.PartiallySettled;
    }
}

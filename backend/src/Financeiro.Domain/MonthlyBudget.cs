namespace Financeiro.Domain;

public enum BudgetTargetKind
{
    Category,
    CostCenter
}

public sealed class MonthlyBudget
{
    public Guid Id { get; init; }
    public Guid WorkspaceId { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public BudgetTargetKind TargetKind { get; init; }
    public Guid TargetId { get; init; }
    public decimal PlannedAmount { get; private set; }
    public int AlertPercentage { get; private set; } = 80;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(decimal plannedAmount, int alertPercentage)
    {
        plannedAmount = FinancialMoney.Positive(plannedAmount);
        if (alertPercentage is < 1 or > 100) throw new ArgumentException("O alerta deve estar entre 1% e 100%.");
        PlannedAmount = plannedAmount;
        AlertPercentage = alertPercentage;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

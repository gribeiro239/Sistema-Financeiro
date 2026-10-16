namespace Financeiro.Domain;

public enum FinancialGoalKind
{
    Revenue,
    ExpenseLimit,
    EmergencyReserve,
    CashBalance,
    Custom
}

public sealed class FinancialGoal
{
    public Guid Id { get; init; }
    public Guid WorkspaceId { get; init; }
    public string Name { get; private set; } = "";
    public FinancialGoalKind Kind { get; private set; }
    public decimal TargetAmount { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public string? Category { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(string name, FinancialGoalKind kind, decimal targetAmount, DateOnly startDate, DateOnly dueDate, string? category)
    {
        name = name.Trim();
        if (name.Length is < 2 or > 100) throw new ArgumentException("O nome deve ter entre 2 e 100 caracteres.");
        targetAmount = FinancialMoney.Positive(targetAmount);
        if (dueDate < startDate) throw new ArgumentException("O prazo deve ser igual ou posterior à data inicial.");
        Name = name; Kind = kind; TargetAmount = targetAmount; StartDate = startDate; DueDate = dueDate;
        Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(); UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetActive(bool active) { IsActive = active; UpdatedAt = DateTimeOffset.UtcNow; }
}

public sealed class GoalContribution
{
    public Guid Id { get; init; }
    public Guid WorkspaceId { get; init; }
    public Guid GoalId { get; init; }
    public decimal Amount { get; init; }
    public DateOnly ContributedOn { get; init; }
    public string Note { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
}

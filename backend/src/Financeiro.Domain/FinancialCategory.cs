namespace Financeiro.Domain;

public enum CategoryKind
{
    Income,
    Expense,
    Both
}

public sealed class FinancialCategory
{
    public Guid Id { get; init; }
    public Guid WorkspaceId { get; init; }
    public Guid? ParentId { get; init; }
    public required string Name { get; init; }
    public CategoryKind Kind { get; init; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; init; }

    public void SetActive(bool active) => IsActive = active;
}

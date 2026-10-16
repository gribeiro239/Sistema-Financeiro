namespace Financeiro.Domain;

public enum DescriptionMatchMode
{
    Contains,
    StartsWith,
    Equals
}

public sealed class CategorizationRule
{
    public Guid Id { get; init; }
    public Guid WorkspaceId { get; init; }
    public required string Name { get; set; }
    public required string DescriptionPattern { get; set; }
    public DescriptionMatchMode MatchMode { get; set; }
    public TransactionType? TransactionType { get; set; }
    public Guid? AccountId { get; set; }
    public required string Category { get; set; }
    public Guid? CostCenterId { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; init; }

    public bool Matches(Guid accountId, string description, TransactionType type)
    {
        if (!IsActive || (AccountId.HasValue && AccountId != accountId) || (TransactionType.HasValue && TransactionType != type)) return false;
        return MatchMode switch
        {
            DescriptionMatchMode.StartsWith => description.StartsWith(DescriptionPattern, StringComparison.OrdinalIgnoreCase),
            DescriptionMatchMode.Equals => description.Equals(DescriptionPattern, StringComparison.OrdinalIgnoreCase),
            _ => description.Contains(DescriptionPattern, StringComparison.OrdinalIgnoreCase)
        };
    }
}

namespace Financeiro.Domain;

public enum AccountType
{
    Checking,
    Savings,
    Cash,
    Investment
}

public sealed class FinancialAccount
{
    public Guid Id { get; init; }
    public Guid WorkspaceId { get; init; }
    public required string Name { get; set; }
    public AccountType Type { get; set; }
    public required string Institution { get; set; }
    public string? LastFourDigits { get; set; }
    public decimal OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;

    public void Update(string name, string institution, string? lastFourDigits, AccountType type, decimal openingBalance)
    {
        Name = name;
        Institution = institution;
        LastFourDigits = lastFourDigits;
        Type = type;
        OpeningBalance = openingBalance;
    }

    public void SetActive(bool active) => IsActive = active;
}

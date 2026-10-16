namespace Financeiro.Domain;

public enum ContactKind { Customer, Supplier, Both }

public sealed class Contact
{
    public Guid Id { get; init; }
    public Guid WorkspaceId { get; init; }
    public required string Name { get; set; }
    public ContactKind Kind { get; set; }
    public string? TaxId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; init; }

    public void Update(string name, ContactKind kind, string? taxId, string? email, string? phone)
    {
        Name = name; Kind = kind; TaxId = taxId; Email = email; Phone = phone;
    }

    public void SetActive(bool active) => IsActive = active;
}

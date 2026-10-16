namespace Financeiro.Domain;

public enum WorkspaceKind
{
    Personal,
    Business
}

public sealed class Workspace
{
    public Guid Id { get; init; }
    public required string Name { get; set; }
    public WorkspaceKind Kind { get; set; }
    public DateTimeOffset CreatedAt { get; init; }

    public void Update(string name, WorkspaceKind kind)
    {
        Name = name;
        Kind = kind;
    }
}

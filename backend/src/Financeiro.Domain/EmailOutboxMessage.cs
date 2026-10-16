namespace Financeiro.Domain;

public sealed class EmailOutboxMessage
{
    public Guid Id { get; init; }
    public required string ProtectedPayload { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}

namespace Financeiro.Domain;

public sealed class DocumentAttachment
{
    public Guid Id { get; init; }
    public Guid WorkspaceId { get; init; }
    public required string EntityType { get; init; }
    public Guid EntityId { get; init; }
    public required string OriginalFileName { get; init; }
    public required string StoredFileName { get; init; }
    public required string ContentType { get; init; }
    public long Size { get; init; }
    public Guid UploadedByUserId { get; init; }
    public DateTimeOffset UploadedAt { get; init; }
}

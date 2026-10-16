namespace Financeiro.Domain;

public enum SupportTicketTarget { WorkspaceAdministration, Support, Development }
public enum SupportTicketPriority { Low, Normal, High, Urgent }
public enum SupportTicketStatus { Open, InProgress, WaitingRequester, Resolved, Closed }

public sealed class SupportTicket
{
    public Guid Id { get; init; }
    public Guid WorkspaceId { get; init; }
    public Guid RequesterUserId { get; init; }
    public SupportTicketTarget Target { get; init; }
    public required string Subject { get; init; }
    public required string Category { get; init; }
    public SupportTicketPriority Priority { get; init; }
    public bool ConsentToShareWorkspaceData { get; init; }
    public SupportTicketStatus Status { get; set; } = SupportTicketStatus.Open;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
}

public sealed class SupportTicketMessage
{
    public Guid Id { get; init; }
    public Guid TicketId { get; init; }
    public Guid AuthorUserId { get; init; }
    public required string Body { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

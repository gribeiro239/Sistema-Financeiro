using System.Security.Claims;
using Financeiro.Application;
using Financeiro.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Financeiro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/help")]
public sealed class HelpController(HelpCenterService service, IWorkspaceContext workspaceContext) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("articles")]
    public ActionResult<IReadOnlyList<HelpArticleView>> GetArticles([FromQuery] string? query, [FromQuery] string? module)
        => Ok(service.GetArticles(query, module));

    [HttpGet("tickets")]
    public async Task<ActionResult<IReadOnlyList<SupportTicketView>>> GetTickets([FromQuery] string scope = "mine", CancellationToken ct = default)
    {
        try { return Ok(await service.GetTicketsAsync(CurrentUserId, workspaceContext.WorkspaceId, scope, ct)); }
        catch (UnauthorizedAccessException exception) { return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = "Acesso negado", Detail = exception.Message }); }
    }

    [HttpPost("tickets")]
    public async Task<ActionResult<SupportTicketView>> CreateTicket([FromBody] CreateSupportTicketRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<SupportTicketTarget>(request.Target, true, out var target)) return BadRequest(new ProblemDetails { Title = "Destino inválido" });
        if (!Enum.TryParse<SupportTicketPriority>(request.Priority, true, out var priority)) return BadRequest(new ProblemDetails { Title = "Prioridade inválida" });
        try
        {
            var result = await service.CreateTicketAsync(CurrentUserId, workspaceContext.WorkspaceId, new CreateSupportTicketCommand(target, request.Subject ?? "", request.Category ?? "", priority, request.Description ?? "", request.ConsentToShareWorkspaceData), ct);
            return Created($"/api/help/tickets/{result.Id}", result);
        }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Chamado inválido", Detail = exception.Message }); }
        catch (UnauthorizedAccessException exception) { return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = "Acesso negado", Detail = exception.Message }); }
    }

    [HttpPost("tickets/{ticketId:guid}/messages")]
    public async Task<ActionResult<SupportTicketView>> AddMessage(Guid ticketId, [FromBody] AddSupportTicketMessageRequest request, CancellationToken ct)
    {
        try { return Ok(await service.AddMessageAsync(CurrentUserId, workspaceContext.WorkspaceId, ticketId, request.Body ?? "", ct)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Chamado não encontrado", Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Mensagem inválida", Detail = exception.Message }); }
        catch (UnauthorizedAccessException exception) { return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = "Acesso negado", Detail = exception.Message }); }
    }

    [HttpPost("tickets/{ticketId:guid}/status")]
    public async Task<ActionResult<SupportTicketView>> SetStatus(Guid ticketId, [FromBody] SetSupportTicketStatusRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<SupportTicketStatus>(request.Status, true, out var status)) return BadRequest(new ProblemDetails { Title = "Situação inválida" });
        try { return Ok(await service.SetStatusAsync(CurrentUserId, workspaceContext.WorkspaceId, ticketId, status, ct)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Chamado não encontrado", Detail = exception.Message }); }
        catch (UnauthorizedAccessException exception) { return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = "Acesso negado", Detail = exception.Message }); }
    }
}

public sealed record CreateSupportTicketRequest(string? Target, string? Subject, string? Category, string? Priority, string? Description, bool ConsentToShareWorkspaceData);
public sealed record AddSupportTicketMessageRequest(string? Body);
public sealed record SetSupportTicketStatusRequest(string? Status);

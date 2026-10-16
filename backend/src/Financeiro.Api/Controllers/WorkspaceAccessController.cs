using System.Security.Claims;
using Financeiro.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Financeiro.Api.Controllers;

[ApiController]
[Authorize]
public sealed class WorkspaceAccessController(WorkspaceAccessService service) : ControllerBase
{
    [HttpGet("api/workspaces/{workspaceId:guid}/members")]
    public async Task<ActionResult<IReadOnlyList<WorkspaceMemberView>>> GetMembers(Guid workspaceId, CancellationToken ct) { try { return Ok(await service.GetMembersAsync(CurrentUserId, workspaceId, ct)); } catch (Exception ex) { return AccessError(ex); } }

    [HttpPost("api/workspaces/{workspaceId:guid}/invitations")]
    public async Task<ActionResult<WorkspaceInvitationView>> Invite(Guid workspaceId, [FromBody] InviteWorkspaceMemberRequest request, CancellationToken ct)
    { if (!Enum.TryParse<WorkspaceRole>(request.Role, true, out var role)) return BadRequest(new ProblemDetails { Title = "Papel inválido" }); try { return Ok(await service.InviteAsync(CurrentUserId, workspaceId, request.Email ?? "", role, ct)); } catch (Exception ex) { return AccessError(ex); } }

    [HttpGet("api/workspaces/{workspaceId:guid}/invitations")]
    public async Task<ActionResult<IReadOnlyList<WorkspaceInvitationView>>> GetWorkspaceInvitations(Guid workspaceId, CancellationToken ct) { try { return Ok(await service.GetWorkspaceInvitationsAsync(CurrentUserId, workspaceId, ct)); } catch (Exception ex) { return AccessError(ex); } }

    [HttpDelete("api/workspaces/{workspaceId:guid}/invitations/{invitationId:guid}")]
    public async Task<IActionResult> RevokeInvitation(Guid workspaceId, Guid invitationId, CancellationToken ct) { try { await service.RevokeInvitationAsync(CurrentUserId, workspaceId, invitationId, ct); return NoContent(); } catch (Exception ex) { return AccessError(ex); } }

    [HttpGet("api/workspace-invitations")]
    public async Task<ActionResult<IReadOnlyList<WorkspaceInvitationView>>> GetInvitations(CancellationToken ct) => Ok(await service.GetInvitationsAsync(CurrentUserId, ct));

    [HttpPost("api/workspace-invitations/{invitationId:guid}/accept")]
    public async Task<IActionResult> AcceptInvitation(Guid invitationId, CancellationToken ct) { try { await service.AcceptInvitationAsync(CurrentUserId, invitationId, ct); return NoContent(); } catch (Exception ex) { return AccessError(ex); } }

    [HttpPut("api/workspaces/{workspaceId:guid}/members/{memberId:guid}/role")]
    public async Task<ActionResult<WorkspaceMemberView>> ChangeRole(Guid workspaceId, Guid memberId, [FromBody] ChangeWorkspaceRoleRequest request, CancellationToken ct)
    { if (!Enum.TryParse<WorkspaceRole>(request.Role, true, out var role)) return BadRequest(new ProblemDetails { Title = "Papel inválido" }); try { return Ok(await service.ChangeRoleAsync(CurrentUserId, workspaceId, memberId, role, ct)); } catch (Exception ex) { return AccessError(ex); } }

    [HttpDelete("api/workspaces/{workspaceId:guid}/members/{memberId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid workspaceId, Guid memberId, CancellationToken ct) { try { await service.RemoveMemberAsync(CurrentUserId, workspaceId, memberId, ct); return NoContent(); } catch (Exception ex) { return AccessError(ex); } }

    [HttpPost("api/workspaces/{workspaceId:guid}/transfer-ownership")]
    public async Task<IActionResult> TransferOwnership(Guid workspaceId, [FromBody] TransferWorkspaceOwnershipRequest request, CancellationToken ct) { try { await service.TransferOwnershipAsync(CurrentUserId, workspaceId, request.NewOwnerId, ct); return NoContent(); } catch (Exception ex) { return AccessError(ex); } }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private ActionResult AccessError(Exception exception) => exception switch { KeyNotFoundException => NotFound(new ProblemDetails { Title = "Registro não encontrado", Detail = exception.Message }), UnauthorizedAccessException => StatusCode(403, new ProblemDetails { Title = "Acesso negado", Detail = exception.Message }), ArgumentException or InvalidOperationException => BadRequest(new ProblemDetails { Title = "Operação inválida", Detail = exception.Message }), _ => throw exception };
}

public sealed record InviteWorkspaceMemberRequest(string? Email, string? Role);
public sealed record ChangeWorkspaceRoleRequest(string? Role);
public sealed record TransferWorkspaceOwnershipRequest(Guid NewOwnerId);

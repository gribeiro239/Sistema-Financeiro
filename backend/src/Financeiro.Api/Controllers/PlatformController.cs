using System.Security.Claims;
using Financeiro.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Financeiro.Api.Controllers;

[ApiController]
[Route("api/platform")]
[Authorize]
[Authorize(Policy = "PrivilegedMfa")]
public sealed class PlatformController(PlatformAdministrationService service) : ControllerBase
{
    [HttpGet("overview")]
    [Authorize(Roles = "SuperAdmin,Support")]
    public async Task<ActionResult<PlatformOverview>> GetOverview(CancellationToken ct) => Ok(await service.GetOverviewAsync(ct));

    [HttpGet("users")]
    [Authorize(Roles = "SuperAdmin,Support")]
    public async Task<ActionResult<IReadOnlyList<PlatformUserView>>> GetUsers(CancellationToken ct) => Ok(await service.GetUsersAsync(ct));

    [HttpGet("workspaces")]
    [Authorize(Roles = "SuperAdmin,Support")]
    public async Task<ActionResult<IReadOnlyList<PlatformWorkspaceView>>> GetWorkspaces(CancellationToken ct) => Ok(await service.GetWorkspacesAsync(ct));

    [HttpPut("users/{userId:guid}/role")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<PlatformUserView>> SetRole(Guid userId, [FromBody] SetGlobalRoleRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<GlobalRole>(request.Role, true, out var role)) return BadRequest(new ProblemDetails { Title = "Papel inválido" });
        try { return Ok(await service.SetRoleAsync(CurrentUserId, userId, role, ct)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Usuário não encontrado", Detail = exception.Message }); }
        catch (InvalidOperationException exception) { return BadRequest(new ProblemDetails { Title = "Alteração não permitida", Detail = exception.Message }); }
    }

    [HttpPost("users/{userId:guid}/active")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<PlatformUserView>> SetActive(Guid userId, [FromBody] SetPlatformUserActiveRequest request, CancellationToken ct)
    {
        try { return Ok(await service.SetActiveAsync(CurrentUserId, userId, request.Active, ct)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Usuário não encontrado", Detail = exception.Message }); }
        catch (InvalidOperationException exception) { return BadRequest(new ProblemDetails { Title = "Alteração não permitida", Detail = exception.Message }); }
    }

    [HttpPost("users/{userId:guid}/password-reset")]
    [Authorize(Roles = "SuperAdmin,Support")]
    public async Task<ActionResult<PasswordRecoveryResult>> IssuePasswordReset(Guid userId, CancellationToken ct)
    {
        try { return Ok(await service.IssuePasswordResetAsync(CurrentUserId, userId, ct)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Usuário não encontrado", Detail = exception.Message }); }
        catch (InvalidOperationException exception) { return BadRequest(new ProblemDetails { Title = "Redefinição indisponível", Detail = exception.Message }); }
    }

    [HttpGet("audit")]
    [Authorize(Roles = "SuperAdmin,Support")]
    public async Task<ActionResult<IReadOnlyList<PlatformAuditView>>> GetAudit(CancellationToken ct) => Ok(await service.GetAuditAsync(ct));

    [HttpGet("diagnostics")]
    [Authorize(Roles = "SuperAdmin,Developer")]
    public async Task<ActionResult<PlatformDiagnosticView>> GetDiagnostics(CancellationToken ct) => Ok(await service.GetDiagnosticsAsync(ct));

    [HttpGet("settings")]
    [Authorize(Roles = "SuperAdmin,Developer")]
    public async Task<ActionResult<IReadOnlyList<PlatformSettingView>>> GetSettings(CancellationToken ct) => Ok(await service.GetSettingsAsync(ct));

    [HttpPut("settings/{key}")]
    [Authorize(Roles = "SuperAdmin,Developer")]
    public async Task<ActionResult<PlatformSettingView>> SaveSetting(string key, [FromBody] SavePlatformSettingRequest request, CancellationToken ct)
    {
        try { return Ok(await service.SaveSettingAsync(CurrentUserId, key, request.Value ?? "", ct)); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Configuração inválida", Detail = exception.Message }); }
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public sealed record SetGlobalRoleRequest(string? Role);
public sealed record SetPlatformUserActiveRequest(bool Active);
public sealed record SavePlatformSettingRequest(string? Value);

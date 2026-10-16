using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Financeiro.Domain;
using Financeiro.Infrastructure;
using System.Security.Claims;

namespace Financeiro.Api.Controllers;

[ApiController]
[Route("api/platform/backups")]
[Authorize(Roles = "SuperAdmin")]
[Authorize(Policy = "PrivilegedMfa")]
public sealed class BackupsController(BackupService backups, FinanceDbContext database) : ControllerBase
{
    [HttpGet]
    public IActionResult List() => Ok(new { backups.Enabled, backups.LastVerifiedAt, backups.LastAttemptFailed, files = backups.List() });

    [HttpPost]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        try { var result = await backups.CreateAsync(ct); await Audit("BackupCreated", result.Name, ct); return Ok(result); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { return StatusCode(503, new ProblemDetails { Title = "Backup indisponível", Detail = "Não foi possível criar e verificar o backup. Consulte os registros operacionais." }); }
    }

    [HttpGet("{name}/download")]
    public async Task<IActionResult> Download(string name, CancellationToken ct)
    {
        try { var path = backups.GetArchivePath(name); await Audit("BackupDownloaded", name, ct); return PhysicalFile(path, "application/octet-stream", name); }
        catch (ArgumentException) { return BadRequest(); }
        catch (FileNotFoundException) { return NotFound(); }
    }

    [HttpPost("{name}/verify")]
    public async Task<IActionResult> Verify(string name, CancellationToken ct)
    {
        try { var result = await backups.VerifyAsync(backups.GetArchivePath(name), ct); await Audit("BackupVerified", name, ct); return Ok(result); }
        catch (FileNotFoundException) { return NotFound(); }
        catch (ArgumentException) { return BadRequest(); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { return UnprocessableEntity(new ProblemDetails { Title = "Verificação falhou", Detail = "O backup não pôde ser validado. Verifique o arquivo e a chave de criptografia." }); }
    }

    private async Task Audit(string action, string name, CancellationToken ct)
    {
        database.PlatformAuditEntries.Add(new PlatformAuditEntry { Id = Guid.NewGuid(),
            ActorUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), Action = action,
            EntityType = "Backup", Description = $"{action}: {name}", IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            OccurredAt = DateTimeOffset.UtcNow });
        await database.SaveChangesAsync(ct);
    }
}

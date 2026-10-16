using Financeiro.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Financeiro.Api.Controllers;

[ApiController]
[Route("api/platform/email")]
[Authorize(Roles = "SuperAdmin")]
[Authorize(Policy = "PrivilegedMfa")]
public sealed class EmailOperationsController(FinanceDbContext database, IOptions<EmailOptions> options) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var rows = await database.EmailOutboxMessages.AsNoTracking()
            .Select(item => new { item.SentAt, item.ExpiresAt, item.Attempts, item.LastError }).ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;
        return Ok(new { options.Value.Enabled,
            pending = rows.Count(item => item.SentAt == null && item.ExpiresAt > now && item.Attempts < 6),
            failed = rows.Count(item => item.SentAt == null && item.ExpiresAt > now && item.Attempts >= 6),
            expired = rows.Count(item => item.SentAt == null && item.ExpiresAt <= now),
            sent = rows.Count(item => item.SentAt != null) });
    }
}

using Financeiro.Application;
using System.Security.Claims;

namespace Financeiro.Api;

public sealed class WorkspaceContext(IHttpContextAccessor httpContextAccessor) : IWorkspaceContext
{
    public const string HeaderName = "X-Workspace-Id";

    public Guid WorkspaceId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.Request.Headers[HeaderName].FirstOrDefault();
            return Guid.TryParse(value, out var workspaceId) ? workspaceId : FinanceService.SeedWorkspaceId;
        }
    }
}

public sealed class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    public Guid UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var userId) ? userId : Guid.Empty;
        }
    }
}

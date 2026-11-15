using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace BankingSystemCore.Security.CurrentUser;

public class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? Id => Guid.TryParse(FindClaim(ClaimNames.Subject), out Guid id) ? id : null;

    public string? Email => FindClaim(ClaimNames.Email);

    public Guid? SessionId => Guid.TryParse(FindClaim(ClaimNames.SessionId), out Guid id) ? id : null;

    public IReadOnlyCollection<string> Roles =>
        Principal?.Claims
            .Where(c => c.Type is ClaimNames.Role or ClaimTypes.Role)
            .Select(c => c.Value)
            .Distinct()
            .ToArray()
        ?? [];

    public bool IsInRole(string role) => Roles.Contains(role);

    public string? FindClaim(string claimType) => IsAuthenticated ? Principal!.FindFirst(claimType)?.Value : null;
}

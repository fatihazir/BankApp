using Application.Services.Security;
using BankingSystemCore.Security.CurrentUser;

namespace Persistence.IntegrationTests.Infrastructure;

/// <summary>
/// The logged-in user of a test. Tests run as bank staff by default; switch with <see cref="AsCustomer"/> or
/// <see cref="AsAnonymous"/> to try the authorization and ownership rules.
/// </summary>
public sealed class TestCurrentUser : ICurrentUser
{
    public bool IsAuthenticated { get; private set; } = true;
    public Guid? Id { get; private set; } = Guid.NewGuid();
    public string? Email { get; private set; } = "test@bank.test";
    public Guid? SessionId { get; private set; } = Guid.NewGuid();
    public IReadOnlyCollection<string> Roles { get; private set; } = [BankRoles.BankStaff];
    public string? IpAddress => "203.0.113.7";

    public bool IsInRole(string role) => Roles.Contains(role);

    public string? FindClaim(string claimType) => null;

    public void AsStaff()
    {
        IsAuthenticated = true;
        Id = Guid.NewGuid();
        Roles = [BankRoles.BankStaff];
    }

    /// <summary>A customer is a user: the customer id is the id of the logged-in user.</summary>
    public void AsCustomer(Guid customerId)
    {
        IsAuthenticated = true;
        Id = customerId;
        Roles = [BankRoles.Customer];
    }

    /// <summary>Makes the test user hold the given session, like a token with that "sid" claim.</summary>
    public void UseSession(Guid sessionId) => SessionId = sessionId;

    public void AsAnonymous()
    {
        IsAuthenticated = false;
        Id = null;
        SessionId = null;
        Roles = [];
    }
}

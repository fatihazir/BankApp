using Application.Services.Security;
using BankingSystemCore.Security.CurrentUser;

namespace Application.UnitTests.Support;

/// <summary>A hand-made current user for tests: staff, a customer, or nobody.</summary>
public sealed class FakeCurrentUser : ICurrentUser
{
    public bool IsAuthenticated { get; private init; }
    public Guid? Id { get; private init; }
    public string? Email { get; private init; }
    public Guid? SessionId { get; private init; }
    public IReadOnlyCollection<string> Roles { get; private init; } = [];
    public string? IpAddress => "203.0.113.7";

    public bool IsInRole(string role) => Roles.Contains(role);

    public string? FindClaim(string claimType) => null;

    public static FakeCurrentUser Staff() => new()
    {
        IsAuthenticated = true, Id = Guid.NewGuid(), Email = "staff@bank.test", SessionId = Guid.NewGuid(), Roles = [BankRoles.BankStaff]
    };

    /// <summary>A customer is a user: the customer id is the id of the logged-in user.</summary>
    public static FakeCurrentUser Customer(Guid customerId) => new()
    {
        IsAuthenticated = true, Id = customerId, Email = "customer@bank.test", SessionId = Guid.NewGuid(), Roles = [BankRoles.Customer]
    };

    /// <summary>A customer token without a user id: must never see any customer data.</summary>
    public static FakeCurrentUser CustomerWithoutId() => new()
    {
        IsAuthenticated = true, Email = "customer@bank.test", Roles = [BankRoles.Customer]
    };

    public static FakeCurrentUser Anonymous() => new();

    public CustomerAccessPolicy Policy() => new(this);
}

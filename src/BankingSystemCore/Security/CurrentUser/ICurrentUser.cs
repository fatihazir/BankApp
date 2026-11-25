namespace BankingSystemCore.Security.CurrentUser;

/// <summary>The user making the current request.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? Id { get; }

    string? Email { get; }

    /// <summary>The session of the current token. Null when the token carries none.</summary>
    Guid? SessionId { get; }

    IReadOnlyCollection<string> Roles { get; }

    /// <summary>Where the request came from (the real client address behind a trusted proxy). Null outside a web request.</summary>
    string? IpAddress { get; }

    bool IsInRole(string role);

    /// <summary>The value of a claim of the token, or null when it is not there.</summary>
    string? FindClaim(string claimType);
}

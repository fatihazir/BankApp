namespace BankingSystemCore.Security.Jwt;

/// <summary>What goes into a token. Independent of any user entity so the core stays reusable.</summary>
public sealed record TokenUser(
    Guid Id,
    string Email,
    IReadOnlyCollection<string> Roles,
    IReadOnlyDictionary<string, string>? ExtraClaims = null,
    Guid? SessionId = null);

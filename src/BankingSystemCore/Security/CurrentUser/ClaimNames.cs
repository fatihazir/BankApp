namespace BankingSystemCore.Security.CurrentUser;

/// <summary>Claim names used in the tokens (the short JWT names, no namespace URIs).</summary>
public static class ClaimNames
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Role = "role";
    public const string TokenId = "jti";

    /// <summary>The session the token belongs to. Revoking the session kills the token at once.</summary>
    public const string SessionId = "sid";
}

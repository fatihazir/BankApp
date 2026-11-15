namespace BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;

/// <summary>
/// Thrown when the caller could not be identified (no token, bad credentials, locked account). Mapped to HTTP 401.
/// </summary>
public class AuthenticationException : Exception
{
    public AuthenticationException(string message) : base(message)
    {
    }
}

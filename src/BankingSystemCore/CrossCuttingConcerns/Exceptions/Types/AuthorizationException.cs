namespace BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;

/// <summary>
/// Thrown when the caller is not allowed to perform an operation. Mapped to HTTP 403.
/// </summary>
public class AuthorizationException : Exception
{
    public AuthorizationException(string message) : base(message)
    {
    }
}

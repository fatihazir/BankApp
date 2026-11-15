namespace BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;

/// <summary>
/// Thrown when a business rule is violated. Mapped to HTTP 400.
/// </summary>
public class BusinessException : Exception
{
    public BusinessException(string message) : base(message)
    {
    }
}

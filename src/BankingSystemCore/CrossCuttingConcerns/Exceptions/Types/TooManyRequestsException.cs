namespace BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;

/// <summary>Thrown when a caller sends requests faster than allowed. Mapped to HTTP 429.</summary>
public class TooManyRequestsException : Exception
{
    public TooManyRequestsException(string message) : base(message)
    {
    }
}

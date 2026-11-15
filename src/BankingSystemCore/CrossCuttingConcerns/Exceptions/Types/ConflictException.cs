namespace BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;

/// <summary>
/// Thrown when the data was changed by somebody else in the meantime (optimistic concurrency). Mapped to HTTP 409.
/// </summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }
}

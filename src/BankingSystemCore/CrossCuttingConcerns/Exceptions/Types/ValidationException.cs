namespace BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;

/// <summary>
/// Thrown when a request fails validation. Carries the errors grouped by property. Mapped to HTTP 400.
/// Independent of any validation library, so the core stays reusable.
/// </summary>
public class ValidationException : Exception
{
    public IEnumerable<ValidationExceptionModel> Errors { get; }

    public ValidationException(IEnumerable<ValidationExceptionModel> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }
}

public class ValidationExceptionModel
{
    public string? Property { get; set; }

    public IEnumerable<string> Errors { get; set; } = [];
}

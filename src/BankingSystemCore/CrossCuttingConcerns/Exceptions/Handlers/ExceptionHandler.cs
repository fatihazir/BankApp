using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;

namespace BankingSystemCore.CrossCuttingConcerns.Exceptions.Handlers;

/// <summary>
/// Turns each exception type into a response. Implement it for a specific transport (e.g. HTTP).
/// </summary>
public abstract class ExceptionHandler
{
    public abstract Task HandleException(BusinessException exception);

    public abstract Task HandleException(ValidationException exception);

    public abstract Task HandleException(AuthenticationException exception);

    public abstract Task HandleException(AuthorizationException exception);

    public abstract Task HandleException(NotFoundException exception);

    public abstract Task HandleException(ConflictException exception);

    /// <summary>Fallback for every exception type that has no dedicated handler.</summary>
    public abstract Task HandleException(Exception exception);
}

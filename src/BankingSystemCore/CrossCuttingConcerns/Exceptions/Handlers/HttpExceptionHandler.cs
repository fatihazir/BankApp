using System.Diagnostics;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.HttpProblemDetails;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ValidationProblemDetails = BankingSystemCore.CrossCuttingConcerns.Exceptions.HttpProblemDetails.ValidationProblemDetails;

namespace BankingSystemCore.CrossCuttingConcerns.Exceptions.Handlers;

/// <summary>
/// Writes RFC 7807 problem details (application/problem+json) to the HTTP response.
/// Create one instance per request and set <see cref="Response"/>.
/// </summary>
public class HttpExceptionHandler : ExceptionHandler
{
    public required HttpResponse Response { get; init; }

    public override Task HandleException(BusinessException exception) =>
        Write(new BusinessProblemDetails(exception.Message));

    public override Task HandleException(ValidationException exception) =>
        Write(new ValidationProblemDetails(exception.Errors));

    public override Task HandleException(AuthenticationException exception) =>
        Write(new AuthenticationProblemDetails(exception.Message));

    public override Task HandleException(AuthorizationException exception) =>
        Write(new AuthorizationProblemDetails(exception.Message));

    public override Task HandleException(NotFoundException exception) =>
        Write(new NotFoundProblemDetails(exception.Message));

    public override Task HandleException(ConflictException exception) =>
        Write(new ConflictProblemDetails(exception.Message));

    /// <summary>The exception message is never returned to the caller, because it may expose internals.</summary>
    public override Task HandleException(Exception exception) =>
        Write(new InternalServerErrorProblemDetails("An unexpected error occurred."));

    private Task Write(ProblemDetails details)
    {
        details.Instance = Response.HttpContext.Request.Path.Value;
        details.Extensions["traceId"] = Activity.Current?.Id ?? Response.HttpContext.TraceIdentifier;

        Response.StatusCode = details.Status ?? StatusCodes.Status500InternalServerError;

        // The runtime type is passed explicitly so derived members (e.g. Errors) are serialized.
        // JSON settings come from the app's HttpJsonOptions.
        return Response.WriteAsJsonAsync(
            details,
            details.GetType(),
            options: null,
            contentType: "application/problem+json",
            cancellationToken: Response.HttpContext.RequestAborted);
    }
}

using BankingSystemCore.CrossCuttingConcerns.Exceptions.Handlers;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BankingSystemCore.CrossCuttingConcerns.Exceptions.Middlewares;

/// <summary>
/// Catches exceptions thrown further down the pipeline and converts them into problem details responses.
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            // Once the response has started, the status code and body can no longer be changed.
            if (context.Response.HasStarted)
                throw;

            await HandleExceptionAsync(context.Response, exception);
        }
    }

    private Task HandleExceptionAsync(HttpResponse response, Exception exception)
    {
        var handler = new HttpExceptionHandler { Response = response };

        switch (exception)
        {
            case BusinessException e:
                return handler.HandleException(e);
            case ValidationException e:
                return handler.HandleException(e);
            case AuthenticationException e:
                return handler.HandleException(e);
            case AuthorizationException e:
                return handler.HandleException(e);
            case NotFoundException e:
                return handler.HandleException(e);
            case ConflictException e:
                return handler.HandleException(e);
            case DbUpdateConcurrencyException:
                // Somebody changed the same row between our read and our write. Nothing was saved.
                return handler.HandleException(new ConflictException("The data was changed by someone else. Reload it and try again."));
            default:
                _logger.LogError(exception, "Unhandled exception");
                return handler.HandleException(exception);
        }
    }
}

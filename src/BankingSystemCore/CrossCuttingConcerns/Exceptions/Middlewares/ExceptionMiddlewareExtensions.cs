using Microsoft.AspNetCore.Builder;

namespace BankingSystemCore.CrossCuttingConcerns.Exceptions.Middlewares;

public static class ExceptionMiddlewareExtensions
{
    /// <summary>Adds <see cref="ExceptionMiddleware"/>. Register it first so it wraps the rest of the pipeline.</summary>
    public static IApplicationBuilder UseCustomExceptionMiddleware(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionMiddleware>();
}

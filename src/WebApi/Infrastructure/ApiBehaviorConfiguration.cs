using System.Diagnostics;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Microsoft.AspNetCore.Mvc;
using ValidationProblemDetails = BankingSystemCore.CrossCuttingConcerns.Exceptions.HttpProblemDetails.ValidationProblemDetails;

namespace WebApi.Infrastructure;

public static class ApiBehaviorConfiguration
{
    /// <summary>
    /// Model binding failures (malformed JSON, wrong types) return the same validation problem format
    /// as FluentValidation failures, instead of the MVC default.
    /// </summary>
    public static IMvcBuilder ConfigureCustomApiBehavior(this IMvcBuilder builder) =>
        builder.ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .Select(entry => new ValidationExceptionModel
                    {
                        // "$.employmentStatus" (JSON path) -> "employmentStatus"
                        Property = entry.Key.TrimStart('$', '.'),
                        // Keys starting with "$" come from the JSON parser, whose messages contain internal
                        // type names and positions, so they are replaced by a generic message.
                        Errors = entry.Key.StartsWith('$')
                            ? ["The value is missing, invalid or has the wrong format."]
                            : entry.Value!.Errors
                                .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "The value is invalid." : e.ErrorMessage)
                                .ToArray()
                    })
                    .ToList();

                var details = new ValidationProblemDetails(errors)
                {
                    Instance = context.HttpContext.Request.Path.Value
                };
                details.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

                return new ObjectResult(details)
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    ContentTypes = { "application/problem+json" }
                };
            };
        });
}

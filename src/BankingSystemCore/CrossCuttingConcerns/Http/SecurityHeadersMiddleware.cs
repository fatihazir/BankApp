using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace BankingSystemCore.CrossCuttingConcerns.Http;

public class SecurityHeadersOptions
{
    /// <summary>An API returns data, never pages, so nothing may be loaded or framed.</summary>
    public string ContentSecurityPolicy { get; set; } = "default-src 'none'; frame-ancestors 'none'";

    /// <summary>Paths that serve pages (for example the Swagger UI in development) and need scripts and styles.</summary>
    public List<string> PathsWithoutContentSecurityPolicy { get; } = [];
}

/// <summary>
/// Adds the response headers that every API response should carry. They are set when the response starts,
/// so error responses and responses of other middleware get them too. A header that was set already is kept.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SecurityHeadersOptions _options;

    public SecurityHeadersMiddleware(RequestDelegate next, SecurityHeadersOptions options)
    {
        _next = next;
        _options = options;
    }

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            IHeaderDictionary headers = context.Response.Headers;

            headers.TryAdd("X-Content-Type-Options", "nosniff");
            headers.TryAdd("X-Frame-Options", "DENY");
            headers.TryAdd("Referrer-Policy", "no-referrer");
            // Responses carry tokens and personal data: no browser or proxy cache may keep them.
            headers.TryAdd("Cache-Control", "no-store");

            bool isPage = _options.PathsWithoutContentSecurityPolicy.Any(p => context.Request.Path.StartsWithSegments(p));
            if (!isPage)
                headers.TryAdd("Content-Security-Policy", _options.ContentSecurityPolicy);

            return Task.CompletedTask;
        });

        return _next(context);
    }
}

public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, Action<SecurityHeadersOptions>? configure = null)
    {
        var options = new SecurityHeadersOptions();
        configure?.Invoke(options);
        return app.UseMiddleware<SecurityHeadersMiddleware>(options);
    }
}

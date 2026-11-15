using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Handlers;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Persistence.Contexts;

namespace WebApi.Infrastructure;

/// <summary>
/// What a host exposed to the internet needs besides the application itself: the real client address behind a proxy,
/// HTTPS enforcement, CORS, a request rate limit on the anonymous endpoints and health endpoints.
/// Everything is closed by default and opened through configuration.
/// </summary>
public static class HostHardening
{
    /// <summary>The policy of the endpoints anybody can call (login, refresh, verify-email).</summary>
    public const string AnonymousAuthPolicy = "anonymous-auth";

    public static IServiceCollection AddHostHardening(this IServiceCollection services, IConfiguration configuration)
    {
        // ---- the real client address behind a reverse proxy / load balancer ----
        // Only proxies that are listed here may set X-Forwarded-*. Everybody else could fake their address
        // and so escape the rate limit. Nothing listed: only a proxy on the same machine is trusted.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;

            foreach (string proxy in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            foreach (string network in configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        });

        // ---- HSTS: browsers must use HTTPS for a year ----
        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(365);
            options.IncludeSubDomains = true;
        });

        // ---- CORS: no origin is allowed unless "Cors:AllowedOrigins" lists it ----
        string[] origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(cors =>
        {
            if (origins.Length > 0)
                cors.AddDefaultPolicy(policy => policy
                    .WithOrigins(origins)
                    .WithMethods("GET", "POST", "PUT", "DELETE")
                    .WithHeaders("Authorization", "Content-Type")
                    .WithExposedHeaders("Retry-After", "api-supported-versions")
                    .SetPreflightMaxAge(TimeSpan.FromHours(1)));
        });

        // ---- rate limit of the anonymous endpoints, per client address ----
        // Account lockout stops password guessing, but alone it lets anybody lock somebody else's account.
        // The limit keeps one address from doing that at speed.
        bool limitEnabled = configuration.GetValue("RateLimiting:Enabled", true);
        int permitLimit = configuration.GetValue("RateLimiting:AnonymousAuth:PermitLimit", 10);
        int windowSeconds = configuration.GetValue("RateLimiting:AnonymousAuth:WindowSeconds", 60);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(AnonymousAuthPolicy, httpContext =>
            {
                if (!limitEnabled)
                    return RateLimitPartition.GetNoLimiter("unlimited");

                IPAddress? address = httpContext.Connection.RemoteIpAddress;
                string client = address is null ? "unknown" : (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();

                return RateLimitPartition.GetFixedWindowLimiter(client, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromSeconds(windowSeconds),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            options.OnRejected = async (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);

                await new HttpExceptionHandler { Response = context.HttpContext.Response }
                    .HandleException(new TooManyRequestsException("Too many requests. Wait a moment and try again."));
            };
        });

        // ---- health: "live" = the process answers, "ready" = the database answers ----
        services.AddHealthChecks().AddDbContextCheck<BankAppDbContext>("database", tags: ["ready"]);

        return services;
    }

    /// <summary>Anonymous on purpose: load balancers call them. They only say Healthy or Unhealthy.</summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new() { Predicate = _ => false });
        endpoints.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") });
        return endpoints;
    }
}

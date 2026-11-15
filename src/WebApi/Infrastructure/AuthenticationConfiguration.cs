using Application.Services.Security;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Handlers;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using BankingSystemCore.Security.CurrentUser;
using BankingSystemCore.Security.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace WebApi.Infrastructure;

public static class AuthenticationConfiguration
{
    /// <summary>
    /// Validates the JWT of every request. Settings are read lazily from <see cref="TokenOptions"/>, so the same
    /// configuration that signs the tokens also validates them.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<TokenOptions>>((bearer, tokenOptions) =>
            {
                bearer.MapInboundClaims = false; // keep the short claim names ("sub", "role")
                bearer.RequireHttpsMetadata = false; // tokens are signed with our own key, there is no metadata endpoint
                bearer.TokenValidationParameters = JwtHelper.CreateValidationParameters(tokenOptions.Value);

                bearer.Events = new JwtBearerEvents
                {
                    // The signature and lifetime are fine at this point. Ask the database whether the session is still alive
                    // and its user is not banned or deleted. This is what makes a ban or a logout work immediately.
                    OnTokenValidated = async context =>
                    {
                        bool hasSession = Guid.TryParse(context.Principal?.FindFirst(ClaimNames.SessionId)?.Value, out Guid sessionId);
                        var validator = context.HttpContext.RequestServices.GetRequiredService<ISessionValidator>();

                        if (!hasSession || !await validator.IsActiveAsync(sessionId, context.HttpContext.RequestAborted))
                            context.Fail("The session has ended.");
                    },

                    // No token, an expired token or a bad signature: answer in the same problem format as every error.
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        await new HttpExceptionHandler { Response = context.Response }.HandleException(
                            new AuthenticationException("Authentication is required. The token is missing, invalid, expired or has been revoked."));
                    }
                };
            });

        return services;
    }
}

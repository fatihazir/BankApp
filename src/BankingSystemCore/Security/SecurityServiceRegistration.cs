using BankingSystemCore.Mailing;
using BankingSystemCore.Security.CurrentUser;
using BankingSystemCore.Security.Hashing;
using BankingSystemCore.Security.Jwt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BankingSystemCore.Security;

public static class SecurityServiceRegistration
{
    /// <summary>
    /// Registers token creation, password hashing and the current-user accessor.
    /// Fails at startup when the JWT settings are missing or weak.
    /// </summary>
    public static IServiceCollection AddSecurityServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TokenOptions>()
            .Bind(configuration.GetSection(TokenOptions.SectionName))
            .Validate(o => o.IsValid(out _), "The JWT settings in 'Security:Jwt' are missing or invalid (the key needs at least 32 bytes).")
            .ValidateOnStart();

        services.AddOptions<PasswordHashingOptions>().Bind(configuration.GetSection(PasswordHashingOptions.SectionName));
        services.AddOptions<MailingOptions>().Bind(configuration.GetSection(MailingOptions.SectionName));

        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();

        services.AddSingleton<ITokenHelper, JwtHelper>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        // Logs instead of sending. Register a real IEmailSender after this call to replace it.
        services.TryAddSingleton<IEmailSender, LoggingEmailSender>();

        return services;
    }
}

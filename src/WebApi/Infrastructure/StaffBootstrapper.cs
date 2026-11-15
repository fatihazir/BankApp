using Application.Services.Repositories;
using BankingSystemCore.Security.Hashing;
using Domain.Entities;

namespace WebApi.Infrastructure;

/// <summary>
/// Creates the first bank staff account, because only staff can create other staff accounts.
/// It runs only when "Security:BootstrapStaff:Email" and "Password" are configured and no staff user exists yet.
/// Remove those settings once the account exists.
/// </summary>
public static class StaffBootstrapper
{
    public static async Task EnsureAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        string? email = configuration["Security:BootstrapStaff:Email"];
        string? password = configuration["Security:BootstrapStaff:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        using IServiceScope scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IApplicationUserRepository>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var time = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(StaffBootstrapper));

        if (await users.AnyAsync(u => u.Role == UserRole.BankStaff, withDeleted: true, cancellationToken: cancellationToken))
            return;

        await users.AddAsync(new ApplicationUser
        {
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = hasher.Hash(password),
            Role = UserRole.BankStaff,
            IsActive = true,
            EmailVerified = true,
            EmailVerifiedDate = time.GetUtcNow().UtcDateTime
        }, cancellationToken);

        logger.LogWarning("The first bank staff user {Email} was created from configuration. Remove the bootstrap settings.", email);
    }
}

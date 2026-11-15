using Application.Features.Auth.Constants;
using Application.Services.Repositories;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;

namespace Application.Features.Auth.Rules;

public class AuthBusinessRules
{
    public const int MaxFailedLoginAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly IApplicationUserRepository _users;

    public AuthBusinessRules(IApplicationUserRepository users)
    {
        _users = users;
    }

    /// <summary>E-mail addresses are compared trimmed and lower-cased.</summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>The e-mail address is the login name of every user, so it is unique across staff and customers.</summary>
    public async Task EmailCannotBeRegisteredTwice(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        // Soft-deleted users are included because the database unique index covers them too.
        if (await _users.AnyAsync(u => u.Email == normalizedEmail, withDeleted: true, cancellationToken: cancellationToken))
            throw new BusinessException(AuthMessages.EmailAlreadyRegistered);
    }
}

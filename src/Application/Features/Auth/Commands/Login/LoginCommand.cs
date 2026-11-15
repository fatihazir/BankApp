using Application.Features.Auth.Constants;
using Application.Features.Auth.Rules;
using Application.Services.Repositories;
using Application.Services.Security;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using BankingSystemCore.Security.Hashing;
using Microsoft.Extensions.Options;
using Domain.Entities;
using MediatR;

namespace Application.Features.Auth.Commands.Login;

/// <summary>Open to everyone: it is how a token is obtained.</summary>
public class LoginCommand : IRequest<LoginResponse>
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse>
{
    // Verified when the e-mail is unknown, so unknown and known e-mails take the same time to answer.
    private static string? _dummyHash;

    private readonly IApplicationUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly AuthTokenIssuer _issuer;
    private readonly TimeProvider _time;
    private readonly EmailVerificationOptions _emailOptions;

    public LoginCommandHandler(
        IApplicationUserRepository users, IPasswordHasher hasher, AuthTokenIssuer issuer, TimeProvider time,
        IOptions<EmailVerificationOptions> emailOptions)
    {
        _users = users;
        _hasher = hasher;
        _issuer = issuer;
        _time = time;
        _emailOptions = emailOptions.Value;
    }

    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        string email = AuthBusinessRules.NormalizeEmail(request.Email);
        DateTime now = _time.GetUtcNow().UtcDateTime;

        ApplicationUser? user = await _users.GetAsync(u => u.Email == email, cancellationToken: cancellationToken);

        if (user is null)
        {
            _dummyHash ??= _hasher.Hash("dummy-password-for-timing");
            _hasher.Verify(request.Password, _dummyHash);
            throw new AuthenticationException(AuthMessages.InvalidCredentials);
        }

        if (user.LockoutEnd is { } lockoutEnd && lockoutEnd > now)
            throw new AuthenticationException(AuthMessages.AccountLocked);

        if (!user.IsActive || !_hasher.Verify(request.Password, user.PasswordHash))
        {
            await RegisterFailedAttemptAsync(user, now, cancellationToken);
            throw new AuthenticationException(AuthMessages.InvalidCredentials);
        }

        // The password was right, so say why a refusal is not about it.
        if (_emailOptions.RequireVerifiedEmail && !user.EmailVerified)
            throw new AuthenticationException(AuthMessages.EmailNotVerified);

        user.FailedLoginCount = 0;
        user.LockoutEnd = null;
        user.LastLoginDate = now;
        await _users.UpdateAsync(user, cancellationToken);

        return await _issuer.StartSessionAsync(user, cancellationToken);
    }

    private async Task RegisterFailedAttemptAsync(ApplicationUser user, DateTime now, CancellationToken cancellationToken)
    {
        user.FailedLoginCount++;

        if (user.FailedLoginCount >= AuthBusinessRules.MaxFailedLoginAttempts)
        {
            user.LockoutEnd = now.Add(AuthBusinessRules.LockoutDuration);
            user.FailedLoginCount = 0;
        }

        await _users.UpdateAsync(user, cancellationToken);
    }
}

using Application.Features.Auth.Constants;
using Application.Features.Auth.Rules;
using Application.Services.Repositories;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using BankingSystemCore.Security.Tokens;
using Domain.Entities;
using MediatR;

namespace Application.Features.Auth.Commands.VerifyEmail;

/// <summary>Open to everyone: it is called from the link in the e-mail, and the token is the credential.</summary>
public class VerifyEmailCommand : IRequest<VerifiedEmailResponse>
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}

public class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand, VerifiedEmailResponse>
{
    private readonly IApplicationUserRepository _users;
    private readonly TimeProvider _time;

    public VerifyEmailCommandHandler(IApplicationUserRepository users, TimeProvider time)
    {
        _users = users;
        _time = time;
    }

    public async Task<VerifiedEmailResponse> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        string email = AuthBusinessRules.NormalizeEmail(request.Email);
        DateTime now = _time.GetUtcNow().UtcDateTime;

        ApplicationUser? user = await _users.GetAsync(u => u.Email == email, cancellationToken: cancellationToken);

        // One message for every way it can fail, so nothing can be learned about existing accounts.
        if (user is null
            || user.EmailVerified
            || !SecureToken.Matches(request.Token, user.EmailVerificationTokenHash)
            || user.EmailVerificationTokenExpiresAt is not { } expiresAt
            || expiresAt <= now)
        {
            throw new BusinessException(AuthMessages.EmailVerificationInvalid);
        }

        user.EmailVerified = true;
        user.EmailVerifiedDate = now;
        user.EmailVerificationTokenHash = null; // one use only
        user.EmailVerificationTokenExpiresAt = null;
        await _users.UpdateAsync(user, cancellationToken);

        return new VerifiedEmailResponse { Email = user.Email, EmailVerified = true };
    }
}

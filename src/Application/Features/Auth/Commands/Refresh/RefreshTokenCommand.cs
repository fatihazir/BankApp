using Application.Features.Auth.Commands.Login;
using Application.Features.Auth.Constants;
using Application.Services.Repositories;
using Application.Services.Security;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using BankingSystemCore.Security.Tokens;
using Domain.Entities;
using MediatR;
using Microsoft.Extensions.Options;

namespace Application.Features.Auth.Commands.Refresh;

/// <summary>Open to everyone: the refresh token itself is the credential.</summary>
public class RefreshTokenCommand : IRequest<LoginResponse>
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, LoginResponse>
{
    private readonly IUserSessionRepository _sessions;
    private readonly IApplicationUserRepository _users;
    private readonly AuthTokenIssuer _issuer;
    private readonly TimeProvider _time;
    private readonly EmailVerificationOptions _emailOptions;

    public RefreshTokenCommandHandler(
        IUserSessionRepository sessions, IApplicationUserRepository users, AuthTokenIssuer issuer, TimeProvider time,
        IOptions<EmailVerificationOptions> emailOptions)
    {
        _sessions = sessions;
        _users = users;
        _issuer = issuer;
        _time = time;
        _emailOptions = emailOptions.Value;
    }

    public async Task<LoginResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        string hash = SecureToken.Hash(request.RefreshToken);
        DateTime now = _time.GetUtcNow().UtcDateTime;

        UserSession? session = await _sessions.GetAsync(
            s => s.RefreshTokenHash == hash || s.PreviousRefreshTokenHash == hash, cancellationToken: cancellationToken);

        if (session is null || session.RevokedDate is not null)
            throw new AuthenticationException(AuthMessages.InvalidRefreshToken);

        // A token that was already replaced is being used again: it was copied. End the whole session,
        // so the thief and the real user both have to log in again.
        if (session.RefreshTokenHash != hash)
        {
            await RevokeAsync(session, "Refresh token reuse detected", now, cancellationToken);
            throw new AuthenticationException(AuthMessages.InvalidRefreshToken);
        }

        if (session.RefreshTokenExpiresAt <= now)
        {
            await RevokeAsync(session, "Refresh token expired", now, cancellationToken);
            throw new AuthenticationException(AuthMessages.InvalidRefreshToken);
        }

        // Read-only: the user is only looked at, never written back (a ban must not be overwritten).
        ApplicationUser? user = await _users.GetAsync(u => u.Id == session.UserId, enableTracking: false, cancellationToken: cancellationToken);

        if (user is null || !user.IsActive)
        {
            await RevokeAsync(session, "User is not active", now, cancellationToken);
            throw new AuthenticationException(AuthMessages.InvalidRefreshToken);
        }

        if (_emailOptions.RequireVerifiedEmail && !user.EmailVerified)
            throw new AuthenticationException(AuthMessages.EmailNotVerified);

        return await _issuer.RotateAsync(session, user, cancellationToken);
    }

    private async Task RevokeAsync(UserSession session, string reason, DateTime now, CancellationToken cancellationToken)
    {
        session.RevokedDate = now;
        session.RevokeReason = reason;
        await _sessions.UpdateAsync(session, cancellationToken);
    }
}

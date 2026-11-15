using Application.Features.Auth.Commands.Login;
using Application.Services.Repositories;
using BankingSystemCore.Security.Jwt;
using BankingSystemCore.Security.Tokens;
using Domain.Entities;
using Microsoft.Extensions.Options;

namespace Application.Services.Security;

/// <summary>
/// Starts sessions (login) and rotates refresh tokens (refresh). Every access token is tied to a session,
/// and every refresh token can be used once.
/// </summary>
public class AuthTokenIssuer
{
    private readonly IUserSessionRepository _sessions;
    private readonly ITokenHelper _tokens;
    private readonly TokenOptions _options;
    private readonly TimeProvider _time;

    public AuthTokenIssuer(
        IUserSessionRepository sessions, ITokenHelper tokens, IOptions<TokenOptions> options, TimeProvider time)
    {
        _sessions = sessions;
        _tokens = tokens;
        _options = options.Value;
        _time = time;
    }

    public async Task<LoginResponse> StartSessionAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        string refreshToken = SecureToken.Create();
        var session = new UserSession
        {
            UserId = user.Id,
            RefreshTokenHash = SecureToken.Hash(refreshToken),
            RefreshTokenExpiresAt = _time.GetUtcNow().UtcDateTime.AddMinutes(_options.RefreshTokenExpirationMinutes)
        };

        await _sessions.AddAsync(session, cancellationToken);

        return Build(user, session, refreshToken);
    }

    /// <summary>Replaces the refresh token of the session. The old one is remembered to detect reuse.</summary>
    public async Task<LoginResponse> RotateAsync(UserSession session, ApplicationUser user, CancellationToken cancellationToken = default)
    {
        DateTime now = _time.GetUtcNow().UtcDateTime;
        string refreshToken = SecureToken.Create();

        session.PreviousRefreshTokenHash = session.RefreshTokenHash;
        session.RefreshTokenHash = SecureToken.Hash(refreshToken);
        session.RefreshTokenExpiresAt = now.AddMinutes(_options.RefreshTokenExpirationMinutes);
        session.LastRefreshDate = now;

        await _sessions.UpdateAsync(session, cancellationToken);

        return Build(user, session, refreshToken);
    }

    private LoginResponse Build(ApplicationUser user, UserSession session, string refreshToken)
    {
        // A customer is a user, so no separate customer claim is needed: the "sub" claim is the customer id.
        AccessToken access = _tokens.CreateToken(
            new TokenUser(user.Id, user.Email, [user.Role.ToString()], SessionId: session.Id));

        return new LoginResponse
        {
            AccessToken = access.Token,
            ExpiresAt = access.ExpiresAt,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAt = session.RefreshTokenExpiresAt,
            UserId = user.Id,
            Email = user.Email,
            Role = user.Role,
            CustomerId = user.Role == UserRole.Customer ? user.Id : null
        };
    }
}

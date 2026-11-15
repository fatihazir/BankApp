using System.Diagnostics;
using BankingSystemCore.Application.Pipelines.Auditing;
using BankingSystemCore.Security.CurrentUser;
using Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Application.Services.Auditing;

/// <summary>
/// The security events nobody is logged in for: logins, refused logins, lockouts, reused refresh tokens.
/// A failing audit store never breaks the login itself; it is logged instead.
/// Attempts on an e-mail that does not exist, and attempts while an account is locked, are not stored:
/// anybody can send those, and they would only fill the table.
/// </summary>
public class SecurityAudit
{
    private readonly IAuditWriter _writer;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<SecurityAudit> _logger;

    public SecurityAudit(IAuditWriter writer, ICurrentUser currentUser, ILogger<SecurityAudit> logger)
    {
        _writer = writer;
        _currentUser = currentUser;
        _logger = logger;
    }

    public Task LoginSucceededAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        WriteAsync("Auth.Login.Succeeded", user.Id, user.Email, user.Role, succeeded: true, detail: null, cancellationToken);

    /// <param name="reason">Why, in a word: "InvalidPassword", "AccountInactive". Never the password.</param>
    public Task LoginFailedAsync(ApplicationUser user, string reason, CancellationToken cancellationToken) =>
        WriteAsync("Auth.Login.Failed", user.Id, user.Email, user.Role, succeeded: false, detail: reason, cancellationToken);

    public Task LockedOutAsync(ApplicationUser user, DateTime lockoutEnd, CancellationToken cancellationToken) =>
        WriteAsync("Auth.Login.LockedOut", user.Id, user.Email, user.Role, succeeded: false,
            detail: $"Locked until {lockoutEnd:O}", cancellationToken);

    /// <summary>A refresh token that was already used came back: it was copied, so the whole session ended.</summary>
    public Task RefreshTokenReusedAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken) =>
        WriteAsync("Auth.Refresh.ReuseDetected", userId, email: null, role: null, succeeded: false,
            detail: $"Session {sessionId} was ended", cancellationToken);

    private async Task WriteAsync(
        string action, Guid userId, string? email, UserRole? role, bool succeeded, string? detail, CancellationToken cancellationToken)
    {
        try
        {
            await _writer.WriteAsync(new AuditEntry
            {
                ActorUserId = userId, ActorEmail = email, ActorRole = role?.ToString(), Action = action, TargetType = "User",
                TargetId = userId, Detail = detail, Succeeded = succeeded, IpAddress = _currentUser.IpAddress,
                TraceId = Activity.Current?.TraceId.ToString()
            }, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogCritical(exception, "The audit entry {Action} for user {UserId} could not be written.", action, userId);
        }
    }
}

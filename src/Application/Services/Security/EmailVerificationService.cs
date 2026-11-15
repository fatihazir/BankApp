using BankingSystemCore.Mailing;
using BankingSystemCore.Security.Tokens;
using BankingSystemCore.Security.Users;
using Microsoft.Extensions.Options;

namespace Application.Services.Security;

/// <summary>
/// Creates e-mail verification tokens and sends the e-mail. With no e-mail provider configured, the
/// <see cref="IEmailSender"/> only logs, so the flow works end to end the day a real provider is plugged in.
/// </summary>
public class EmailVerificationService
{
    private readonly IEmailSender _sender;
    private readonly EmailVerificationOptions _options;
    private readonly TimeProvider _time;

    public EmailVerificationService(IEmailSender sender, IOptions<EmailVerificationOptions> options, TimeProvider time)
    {
        _sender = sender;
        _options = options.Value;
        _time = time;
    }

    /// <summary>Stores the hash of a new token on the user (not saved yet) and returns the token for the e-mail.</summary>
    public string IssueToken(User user)
    {
        string token = SecureToken.Create();
        user.EmailVerificationTokenHash = SecureToken.Hash(token);
        user.EmailVerificationTokenExpiresAt = _time.GetUtcNow().UtcDateTime.AddHours(_options.TokenLifetimeHours);
        return token;
    }

    public Task SendAsync(User user, string token, CancellationToken cancellationToken = default)
    {
        string link = _options.LinkTemplate
            .Replace("{email}", Uri.EscapeDataString(user.Email))
            .Replace("{token}", Uri.EscapeDataString(token));

        var message = new EmailMessage(
            user.Email,
            "Verify your e-mail address",
            $"Open this link to verify your e-mail address: {link}{Environment.NewLine}" +
            $"The link is valid for {_options.TokenLifetimeHours} hours. If you did not create an account, ignore this e-mail.");

        return _sender.SendAsync(message, cancellationToken);
    }
}

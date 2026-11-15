using System.Text.RegularExpressions;
using BankingSystemCore.Mailing;

namespace WebApi.IntegrationTests.Infrastructure;

/// <summary>Keeps the e-mails instead of sending them, so a test can read the verification token like a user would.</summary>
public sealed partial class CapturingEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _sent = [];

    public IReadOnlyList<EmailMessage> Sent => _sent;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _sent.Add(message);
        return Task.CompletedTask;
    }

    /// <summary>The token in the link of the newest e-mail sent to the address.</summary>
    public string TokenSentTo(string email)
    {
        EmailMessage message = _sent.Last(m => m.To == email);
        return Uri.UnescapeDataString(TokenPattern().Match(message.Body).Groups[1].Value);
    }

    [GeneratedRegex(@"token=([^&\s]+)")]
    private static partial Regex TokenPattern();
}

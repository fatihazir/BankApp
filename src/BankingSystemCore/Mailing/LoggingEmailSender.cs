using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BankingSystemCore.Mailing;

/// <summary>
/// The default sender: it does not send anything, it only logs. Used until a real provider is configured.
/// </summary>
public class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;
    private readonly MailingOptions _options;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger, IOptions<MailingOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "No e-mail provider is configured, so this e-mail was NOT sent. To: {To}, Subject: {Subject}",
            message.To, message.Subject);

        if (_options.LogBody)
            _logger.LogInformation("E-mail body for {To}:{NewLine}{Body}", message.To, Environment.NewLine, message.Body);

        return Task.CompletedTask;
    }
}

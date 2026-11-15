namespace BankingSystemCore.Mailing;

/// <summary>
/// Sends e-mail. Replace the default <see cref="LoggingEmailSender"/> with a real implementation (SMTP, SendGrid, ...)
/// by registering it after <c>AddSecurityServices</c>.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

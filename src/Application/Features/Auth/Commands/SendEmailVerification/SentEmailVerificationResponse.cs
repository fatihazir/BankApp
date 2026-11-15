namespace Application.Features.Auth.Commands.SendEmailVerification;

public class SentEmailVerificationResponse
{
    public string Email { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

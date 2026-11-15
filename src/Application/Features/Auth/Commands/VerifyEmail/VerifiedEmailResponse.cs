namespace Application.Features.Auth.Commands.VerifyEmail;

public class VerifiedEmailResponse
{
    public string Email { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
}

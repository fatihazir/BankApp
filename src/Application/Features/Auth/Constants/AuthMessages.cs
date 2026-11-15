namespace Application.Features.Auth.Constants;

public static class AuthMessages
{
    public const string InvalidCredentials = "Invalid e-mail or password.";
    public const string EmailNotVerified = "The e-mail address is not verified. Open the link that was sent to you.";
    public const string InvalidRefreshToken = "The refresh token is invalid, expired or already used. Log in again.";
    public const string EmailVerificationInvalid = "The verification link is invalid or has expired.";
    public const string EmailAlreadyVerified = "The e-mail address is already verified.";
    public const string CannotBanYourself = "You cannot ban your own account.";
    public const string AlreadyBanned = "The user is already banned.";
    public const string UserNotFound = "User was not found.";
    public const string AccountLocked = "The account is temporarily locked because of too many failed attempts. Try again later.";
    public const string EmailAlreadyRegistered = "This e-mail address is already registered.";
}

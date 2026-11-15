using Domain.Entities;

namespace Application.Features.Auth.Commands.Login;

public class LoginResponse
{
    /// <summary>Send it on every request as <c>Authorization: Bearer {accessToken}</c>.</summary>
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public DateTime ExpiresAt { get; set; }

    /// <summary>Exchange it for a new token pair at <c>POST /api/auth/refresh</c>. It can be used once.</summary>
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; set; }

    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public Guid? CustomerId { get; set; }
}

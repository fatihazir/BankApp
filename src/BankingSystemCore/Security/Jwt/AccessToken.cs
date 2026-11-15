namespace BankingSystemCore.Security.Jwt;

public sealed record AccessToken(string Token, DateTime ExpiresAt);

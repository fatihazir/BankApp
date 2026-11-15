using System.Security.Claims;
using System.Text;
using BankingSystemCore.Security.CurrentUser;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BankingSystemCore.Security.Jwt;

public class JwtHelper : ITokenHelper
{
    private readonly TokenOptions _options;
    private readonly TimeProvider _time;

    public JwtHelper(IOptions<TokenOptions> options, TimeProvider? time = null)
    {
        _options = options.Value;
        _time = time ?? TimeProvider.System;
    }

    public AccessToken CreateToken(TokenUser user)
    {
        DateTime now = _time.GetUtcNow().UtcDateTime;
        DateTime expires = now.AddMinutes(_options.AccessTokenExpirationMinutes);

        var claims = new List<Claim>
        {
            new(ClaimNames.Subject, user.Id.ToString()),
            new(ClaimNames.Email, user.Email),
            new(ClaimNames.TokenId, Guid.NewGuid().ToString())
        };
        if (user.SessionId is { } sessionId)
            claims.Add(new Claim(ClaimNames.SessionId, sessionId.ToString()));
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimNames.Role, role)));
        if (user.ExtraClaims is not null)
            claims.AddRange(user.ExtraClaims.Select(c => new Claim(c.Key, c.Value)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now,
            IssuedAt = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(CreateKey(_options), SecurityAlgorithms.HmacSha256)
        };

        return new AccessToken(new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }

    /// <summary>The rules every incoming token must pass. Shared by the API and by tests.</summary>
    public static TokenValidationParameters CreateValidationParameters(TokenOptions options) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = options.Issuer,
        ValidateAudience = true,
        ValidAudience = options.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = CreateKey(options),
        ValidateLifetime = true,
        RequireExpirationTime = true,
        RequireSignedTokens = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = ClaimNames.Subject,
        RoleClaimType = ClaimNames.Role
    };

    private static SymmetricSecurityKey CreateKey(TokenOptions options) => new(Encoding.UTF8.GetBytes(options.SecurityKey));
}

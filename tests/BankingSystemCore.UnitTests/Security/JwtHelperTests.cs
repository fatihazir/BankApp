using BankingSystemCore.Security.Jwt;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BankingSystemCore.UnitTests.Security;

public class JwtHelperTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static TokenOptions Options(string? key = null, int minutes = 30) => new()
    {
        Issuer = "issuer", Audience = "audience", SecurityKey = key ?? new string('k', 40), AccessTokenExpirationMinutes = minutes
    };

    private static JwtHelper Helper(TokenOptions options, DateTimeOffset? now = null) =>
        new(Microsoft.Extensions.Options.Options.Create(options), new FixedTime(now ?? Now));

    private static readonly TokenUser User = new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"), "user@bank.test", ["Customer"],
        new Dictionary<string, string> { ["customerId"] = "22222222-2222-2222-2222-222222222222" });

    private static async Task<TokenValidationResult> Validate(string token, TokenOptions options, DateTimeOffset? at = null)
    {
        var parameters = JwtHelper.CreateValidationParameters(options);
        DateTime moment = (at ?? Now).UtcDateTime;

        // Same rule as the real validation (including the clock skew), evaluated at a chosen moment.
        parameters.LifetimeValidator = (notBefore, expires, _, p) =>
            (notBefore is null || moment >= notBefore.Value - p.ClockSkew) && (expires is null || moment <= expires.Value + p.ClockSkew);

        return await new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);
    }

    [Fact]
    public async Task Token_ContainsTheUserRolesAndExtraClaims()
    {
        var options = Options();
        var token = Helper(options).CreateToken(User);

        var result = await Validate(token.Token, options);

        result.IsValid.ShouldBeTrue();
        result.ClaimsIdentity.FindFirst("sub")!.Value.ShouldBe(User.Id.ToString());
        result.ClaimsIdentity.FindFirst("email")!.Value.ShouldBe("user@bank.test");
        result.ClaimsIdentity.FindFirst("role")!.Value.ShouldBe("Customer");
        result.ClaimsIdentity.FindFirst("customerId")!.Value.ShouldBe("22222222-2222-2222-2222-222222222222");
        result.ClaimsIdentity.FindFirst("jti").ShouldNotBeNull();
    }

    [Fact]
    public async Task Token_WithSeveralRoles_HasOneRoleClaimEach()
    {
        var options = Options();
        var token = Helper(options).CreateToken(User with { Roles = ["Customer", "BankStaff"] });

        var result = await Validate(token.Token, options);

        result.ClaimsIdentity.FindAll("role").Select(c => c.Value).ShouldBe(["Customer", "BankStaff"], ignoreOrder: true);
    }

    [Fact]
    public async Task Token_CarriesTheSessionId_WhenThereIsOne()
    {
        var options = Options();
        Guid sessionId = Guid.NewGuid();

        var token = Helper(options).CreateToken(User with { SessionId = sessionId });

        (await Validate(token.Token, options)).ClaimsIdentity.FindFirst("sid")!.Value.ShouldBe(sessionId.ToString());
    }

    [Fact]
    public async Task Token_HasNoSessionClaim_WhenThereIsNoSession()
    {
        var options = Options();

        (await Validate(Helper(options).CreateToken(User).Token, options)).ClaimsIdentity.FindFirst("sid").ShouldBeNull();
    }

    [Fact]
    public void Token_ExpiresAfterTheConfiguredTime()
    {
        var token = Helper(Options(minutes: 45)).CreateToken(User);

        token.ExpiresAt.ShouldBe(Now.UtcDateTime.AddMinutes(45));
    }

    [Fact]
    public async Task Token_IsAcceptedBeforeItExpires_AndRefusedAfter()
    {
        var options = Options(minutes: 30);
        string token = Helper(options).CreateToken(User).Token;

        (await Validate(token, options, Now.AddMinutes(29))).IsValid.ShouldBeTrue();
        (await Validate(token, options, Now.AddMinutes(32))).IsValid.ShouldBeFalse(); // 1 minute clock skew allowed
    }

    [Fact]
    public async Task Token_SignedWithAnotherKey_IsRefused()
    {
        string token = Helper(Options(new string('a', 40))).CreateToken(User).Token;

        (await Validate(token, Options(new string('b', 40)))).IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Token_ForAnotherIssuerOrAudience_IsRefused()
    {
        string token = Helper(Options()).CreateToken(User).Token;

        (await Validate(token, new TokenOptions { Issuer = "evil", Audience = "audience", SecurityKey = new string('k', 40) })).IsValid.ShouldBeFalse();
        (await Validate(token, new TokenOptions { Issuer = "issuer", Audience = "other", SecurityKey = new string('k', 40) })).IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task TamperedPayload_IsRefused()
    {
        string token = Helper(Options()).CreateToken(User).Token;
        string[] parts = token.Split('.');
        string payload = System.Text.Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1])).Replace("Customer", "BankStaff");
        parts[1] = Base64UrlEncoder.Encode(payload);

        (await Validate(string.Join('.', parts), Options())).IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task UnsignedToken_AlgNone_IsRefused()
    {
        string header = Base64UrlEncoder.Encode("""{"alg":"none","typ":"JWT"}""");
        string payload = Base64UrlEncoder.Encode("""{"sub":"x","role":"BankStaff","iss":"issuer","aud":"audience","exp":9999999999}""");

        (await Validate($"{header}.{payload}.", Options())).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void EveryTokenGetsItsOwnId()
    {
        var helper = Helper(Options());
        var handler = new JsonWebTokenHandler();

        string first = handler.ReadJsonWebToken(helper.CreateToken(User).Token).Id;
        string second = handler.ReadJsonWebToken(helper.CreateToken(User).Token).Id;

        first.ShouldNotBe(second);
    }

    // ---- options ----

    [Fact]
    public void Options_AreValid_WhenCompleteAndTheKeyIsLongEnough() =>
        Options().IsValid(out _).ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("too-short")]
    [InlineData("0123456789012345678901234567890")] // 31 bytes
    public void Options_WithAWeakKey_AreInvalid(string key)
    {
        Options(key).IsValid(out string error).ShouldBeFalse();
        error.ShouldContain("32 bytes");
    }

    [Fact]
    public void Options_ExactlyThirtyTwoBytes_AreValid() => Options(new string('k', 32)).IsValid(out _).ShouldBeTrue();

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1441)]
    public void Options_WithAnUnreasonableLifetime_AreInvalid(int minutes) => Options(minutes: minutes).IsValid(out _).ShouldBeFalse();

    [Fact]
    public void TheDefaults_AreAShortAccessToken_AndAThirtyMinuteRefreshToken()
    {
        var options = new TokenOptions();

        options.AccessTokenExpirationMinutes.ShouldBe(10);
        options.RefreshTokenExpirationMinutes.ShouldBe(30);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(43201)]
    public void Options_WithAnUnreasonableRefreshLifetime_AreInvalid(int minutes)
    {
        var options = Options();
        options.RefreshTokenExpirationMinutes = minutes;

        options.IsValid(out string error).ShouldBeFalse();
        error.ShouldContain("RefreshTokenExpirationMinutes");
    }

    [Fact]
    public void Options_WithoutIssuerOrAudience_AreInvalid()
    {
        var noIssuer = Options(); noIssuer.Issuer = "";
        var noAudience = Options(); noAudience.Audience = " ";

        noIssuer.IsValid(out _).ShouldBeFalse();
        noAudience.IsValid(out _).ShouldBeFalse();
    }
}

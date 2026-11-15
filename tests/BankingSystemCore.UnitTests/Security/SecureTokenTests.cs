using BankingSystemCore.Security.Tokens;

namespace BankingSystemCore.UnitTests.Security;

public class SecureTokenTests
{
    [Fact]
    public void Tokens_AreRandom_LongEnough_AndSafeInAUrl()
    {
        var tokens = Enumerable.Range(0, 500).Select(_ => SecureToken.Create()).ToList();

        tokens.Distinct().Count().ShouldBe(500);
        tokens.ShouldAllBe(t => t.Length >= 43);                                    // 256 bits in Base64Url
        // no +, / or = characters
        tokens.ShouldAllBe(t => t.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_'));
    }

    [Fact]
    public void Hash_IsDeterministic_FixedLength_AndDoesNotContainTheToken()
    {
        string token = SecureToken.Create();

        SecureToken.Hash(token).ShouldBe(SecureToken.Hash(token));
        SecureToken.Hash(token).Length.ShouldBe(64);
        SecureToken.Hash(token).ShouldNotContain(token);
        SecureToken.Hash(token).ShouldNotBe(SecureToken.Hash(token + "x"));
    }

    [Fact]
    public void Matches_AcceptsOnlyTheRightToken()
    {
        string token = SecureToken.Create();
        string hash = SecureToken.Hash(token);

        SecureToken.Matches(token, hash).ShouldBeTrue();
        SecureToken.Matches(token + "x", hash).ShouldBeFalse();
        SecureToken.Matches(SecureToken.Create(), hash).ShouldBeFalse();
        SecureToken.Matches("", hash).ShouldBeFalse();
    }

    [Fact]
    public void Matches_WithNoStoredHash_IsFalse() => SecureToken.Matches("anything", null).ShouldBeFalse();

    [Fact]
    public void Matches_WithAHashOfAnotherLength_IsFalse_InsteadOfThrowing() =>
        SecureToken.Matches("anything", "short").ShouldBeFalse();
}

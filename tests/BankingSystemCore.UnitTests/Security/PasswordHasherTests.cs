using BankingSystemCore.Security.Hashing;
using Microsoft.Extensions.Options;

namespace BankingSystemCore.UnitTests.Security;

public class PasswordHasherTests
{
    private static Pbkdf2PasswordHasher Hasher(int iterations = 10) =>
        new(Options.Create(new PasswordHashingOptions { Iterations = iterations }));

    [Fact]
    public void Hash_ThenVerify_Succeeds()
    {
        var hasher = Hasher();

        hasher.Verify("Correct-Horse-9", hasher.Hash("Correct-Horse-9")).ShouldBeTrue();
    }

    [Theory]
    [InlineData("correct-horse-9")]
    [InlineData("Correct-Horse-9 ")]
    [InlineData("Wrong")]
    [InlineData("")]
    public void Verify_WithAnyOtherPassword_Fails(string attempt)
    {
        var hasher = Hasher();

        hasher.Verify(attempt, hasher.Hash("Correct-Horse-9")).ShouldBeFalse();
    }

    [Fact]
    public void Hash_IsSalted_SamePasswordGivesDifferentHashes_ThatBothVerify()
    {
        var hasher = Hasher();

        string first = hasher.Hash("Correct-Horse-9");
        string second = hasher.Hash("Correct-Horse-9");

        first.ShouldNotBe(second);
        hasher.Verify("Correct-Horse-9", first).ShouldBeTrue();
        hasher.Verify("Correct-Horse-9", second).ShouldBeTrue();
    }

    [Fact]
    public void Hash_NeverContainsThePassword()
    {
        Hasher().Hash("Correct-Horse-9").ShouldNotContain("Correct-Horse-9");
    }

    [Fact]
    public void Hash_StoresTheAlgorithmAndTheCost_SoTheCostCanBeRaisedLater()
    {
        string hash = Hasher(iterations: 77).Hash("x");

        hash.ShouldStartWith("PBKDF2-SHA256$77$");
        hash.Split('$').Length.ShouldBe(4);
    }

    [Fact]
    public void Verify_UsesTheCostStoredInTheHash_NotTheCostOfTheCurrentHasher()
    {
        string oldHash = Hasher(iterations: 5).Hash("Correct-Horse-9");

        Hasher(iterations: 1_000).Verify("Correct-Horse-9", oldHash).ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a hash")]
    [InlineData("PBKDF2-SHA256$10$onlythree")]
    [InlineData("MD5$10$AAAA$BBBB")]
    [InlineData("PBKDF2-SHA256$abc$AAAA$BBBB")]
    [InlineData("PBKDF2-SHA256$0$AAAA$BBBB")]
    [InlineData("PBKDF2-SHA256$10$***$***")]
    public void Verify_WithAMalformedHash_ReturnsFalse_InsteadOfThrowing(string malformed) =>
        Hasher().Verify("anything", malformed).ShouldBeFalse();

    [Fact]
    public void Verify_WithATamperedHash_Fails()
    {
        var hasher = Hasher();
        string[] parts = hasher.Hash("Correct-Horse-9").Split('$');
        byte[] bytes = Convert.FromBase64String(parts[3]);
        bytes[0] ^= 0xFF;
        parts[3] = Convert.ToBase64String(bytes);

        hasher.Verify("Correct-Horse-9", string.Join('$', parts)).ShouldBeFalse();
    }

    [Fact]
    public void Hash_WithAnEmptyPassword_Throws() =>
        Should.Throw<ArgumentException>(() => Hasher().Hash(""));

    [Fact]
    public void TheDefaultCost_IsAtLeastTheOwaspRecommendation() =>
        new PasswordHashingOptions().Iterations.ShouldBeGreaterThanOrEqualTo(600_000);
}

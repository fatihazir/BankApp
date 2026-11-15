using Application.Features.Auth.Commands.SendEmailVerification;
using Application.Features.Auth.Commands.VerifyEmail;
using Application.Services.Security;
using Application.UnitTests.Support;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using BankingSystemCore.Mailing;
using BankingSystemCore.Security.Tokens;
using Domain.Entities;
using FluentValidation.TestHelper;
using NSubstitute;

namespace Application.UnitTests.Features.Auth;

public class EmailVerificationTests
{
    private readonly AuthFixture _f = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private VerifyEmailCommandHandler Verify() => new(_f.Users, _f.Time);

    private ApplicationUser PendingUser(out string token)
    {
        var user = _f.Staff();
        user.EmailVerified = false;
        token = _f.Verification.IssueToken(user);
        _f.UserIs(user);
        return user;
    }

    // ---- the token ----

    [Fact]
    public void IssueToken_StoresOnlyAHash_AndAnExpiry()
    {
        var user = _f.Staff();

        string token = _f.Verification.IssueToken(user);

        user.EmailVerificationTokenHash.ShouldBe(SecureToken.Hash(token));
        user.EmailVerificationTokenHash!.ShouldNotContain(token);
        user.EmailVerificationTokenExpiresAt.ShouldBe(_f.Utc.AddHours(24));
    }

    [Fact]
    public void EveryToken_IsDifferent_AndANewOneReplacesTheOldOne()
    {
        var user = _f.Staff();

        string first = _f.Verification.IssueToken(user);
        string second = _f.Verification.IssueToken(user);

        first.ShouldNotBe(second);
        SecureToken.Matches(first, user.EmailVerificationTokenHash).ShouldBeFalse();
        SecureToken.Matches(second, user.EmailVerificationTokenHash).ShouldBeTrue();
    }

    [Fact]
    public async Task TheEmail_ContainsALinkWithTheEscapedAddressAndTheToken()
    {
        var user = _f.Staff();
        user.Email = "ali+test@bank.test";

        await _f.Verification.SendAsync(user, "TOKEN-123", Ct);

        await _f.Sender.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.To == "ali+test@bank.test"
                                      && m.Subject.Contains("Verify")
                                      && m.Body.Contains("email=ali%2Btest%40bank.test")
                                      && m.Body.Contains("token=TOKEN-123")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheLinkTemplate_AndTheLifetime_AreConfigurable()
    {
        _f.EmailOptions.LinkTemplate = "https://bank.example/verify/{token}?e={email}";
        _f.EmailOptions.TokenLifetimeHours = 2;
        var user = _f.Staff();

        string token = _f.Verification.IssueToken(user);
        await _f.Verification.SendAsync(user, token, Ct);

        user.EmailVerificationTokenExpiresAt.ShouldBe(_f.Utc.AddHours(2));
        await _f.Sender.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.Body.Contains($"https://bank.example/verify/{token}?e=staff%40bank.test") && m.Body.Contains("2 hours")),
            Arg.Any<CancellationToken>());
    }

    // ---- verifying ----

    [Fact]
    public async Task TheRightToken_VerifiesTheAddress_AndCannotBeUsedAgain()
    {
        var user = PendingUser(out string token);

        var result = await Verify().Handle(new VerifyEmailCommand { Email = "STAFF@bank.test", Token = token }, Ct);

        result.EmailVerified.ShouldBeTrue();
        user.EmailVerified.ShouldBeTrue();
        user.EmailVerifiedDate.ShouldBe(_f.Utc);
        (user.EmailVerificationTokenHash, user.EmailVerificationTokenExpiresAt).ShouldBe((null, null));
        await _f.Users.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());

        await Should.ThrowAsync<BusinessException>(() => Verify().Handle(new VerifyEmailCommand { Email = user.Email, Token = token }, Ct));
    }

    [Fact]
    public async Task AWrongToken_IsRefused_AndNothingChanges()
    {
        var user = PendingUser(out _);

        await Should.ThrowAsync<BusinessException>(() => Verify().Handle(new VerifyEmailCommand { Email = user.Email, Token = "wrong" }, Ct));

        user.EmailVerified.ShouldBeFalse();
        user.EmailVerificationTokenHash.ShouldNotBeNull();
        await _f.Users.DidNotReceiveWithAnyArgs().UpdateAsync(default!, Ct);
    }

    [Fact]
    public async Task AnExpiredToken_IsRefused()
    {
        var user = PendingUser(out string token);
        _f.Time.Now = _f.Time.Now.AddHours(24).AddSeconds(1);

        await Should.ThrowAsync<BusinessException>(() => Verify().Handle(new VerifyEmailCommand { Email = user.Email, Token = token }, Ct));

        user.EmailVerified.ShouldBeFalse();
    }

    [Fact]
    public async Task AToken_IsStillGoodJustBeforeItExpires()
    {
        var user = PendingUser(out string token);
        _f.Time.Now = _f.Time.Now.AddHours(23).AddMinutes(59);

        (await Verify().Handle(new VerifyEmailCommand { Email = user.Email, Token = token }, Ct)).EmailVerified.ShouldBeTrue();
    }

    [Fact]
    public async Task AnUnknownEmail_AWrongToken_AndAnExpiredToken_AllGiveTheSameMessage()
    {
        _f.UserIs(null);
        var unknown = await Should.ThrowAsync<BusinessException>(() => Verify().Handle(new VerifyEmailCommand { Email = "x@y.com", Token = "t" }, Ct));

        var user = PendingUser(out string token);
        var wrong = await Should.ThrowAsync<BusinessException>(() => Verify().Handle(new VerifyEmailCommand { Email = user.Email, Token = "t" }, Ct));

        _f.Time.Now = _f.Time.Now.AddDays(2);
        var expired = await Should.ThrowAsync<BusinessException>(() => Verify().Handle(new VerifyEmailCommand { Email = user.Email, Token = token }, Ct));

        new[] { unknown.Message, wrong.Message, expired.Message }.Distinct().Count().ShouldBe(1);
    }

    [Fact]
    public async Task AnAlreadyVerifiedUser_CannotUseAToken()
    {
        var user = PendingUser(out string token);
        user.EmailVerified = true;

        await Should.ThrowAsync<BusinessException>(() => Verify().Handle(new VerifyEmailCommand { Email = user.Email, Token = token }, Ct));
    }

    [Fact]
    public async Task AUserWithoutAnyToken_CannotBeVerified()
    {
        var user = _f.Staff(); user.EmailVerified = false;
        _f.UserIs(user);

        await Should.ThrowAsync<BusinessException>(() => Verify().Handle(new VerifyEmailCommand { Email = user.Email, Token = "" }, Ct));
    }

    // ---- asking for a new e-mail ----

    private SendEmailVerificationCommandHandler Send(FakeCurrentUser user) => new(user, _f.Users, _f.Verification);

    [Fact]
    public async Task ARequest_SendsANewEmail_AndInvalidatesTheOldToken()
    {
        var user = PendingUser(out string oldToken);
        var current = FakeCurrentUser.Staff();

        var result = await Send(current).Handle(new SendEmailVerificationCommand(), Ct);

        result.Email.ShouldBe(user.Email);
        result.ExpiresAt.ShouldBe(_f.Utc.AddHours(24));
        SecureToken.Matches(oldToken, user.EmailVerificationTokenHash).ShouldBeFalse();
        await _f.Users.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        await _f.Sender.Received(1).SendAsync(Arg.Is<EmailMessage>(m => m.To == user.Email), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AVerifiedUser_DoesNotGetAnotherEmail()
    {
        var user = _f.Staff();
        _f.UserIs(user);

        await Should.ThrowAsync<BusinessException>(() => Send(FakeCurrentUser.Staff()).Handle(new SendEmailVerificationCommand(), Ct));

        await _f.Sender.DidNotReceiveWithAnyArgs().SendAsync(default!, Ct);
    }

    [Fact]
    public async Task WithoutAUser_ItIsAnAuthenticationError()
    {
        await Should.ThrowAsync<AuthenticationException>(() => Send(FakeCurrentUser.Anonymous()).Handle(new SendEmailVerificationCommand(), Ct));
    }

    [Fact]
    public async Task ADeletedUser_IsNotFound()
    {
        _f.UserIs(null);

        await Should.ThrowAsync<NotFoundException>(() => Send(FakeCurrentUser.Staff()).Handle(new SendEmailVerificationCommand(), Ct));
    }

    [Fact]
    public void VerifyValidator_ChecksEmailAndToken()
    {
        var validator = new VerifyEmailCommandValidator();

        validator.TestValidate(new VerifyEmailCommand { Email = "a@b.com", Token = "t" }).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new VerifyEmailCommand { Email = "bad", Token = "t" }).ShouldHaveValidationErrorFor(c => c.Email);
        validator.TestValidate(new VerifyEmailCommand { Email = "a@b.com" }).ShouldHaveValidationErrorFor(c => c.Token);
    }
}

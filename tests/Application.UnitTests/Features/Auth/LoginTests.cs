using System.Linq.Expressions;
using Application.Features.Auth.Commands.Login;
using Application.Features.Auth.Rules;
using Application.UnitTests.Support;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using BankingSystemCore.Security.Tokens;
using Domain.Entities;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.IdentityModel.JsonWebTokens;
using NSubstitute;

namespace Application.UnitTests.Features.Auth;

public class LoginTests
{
    private readonly AuthFixture _f = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private LoginCommandHandler Handler() => new(_f.Users, _f.Hasher, _f.Issuer, _f.Time, _f.EmailOptionsAccessor, _f.Audit);

    private static LoginCommand Login(string password = AuthFixture.Password, string email = "Staff@Bank.test ") =>
        new() { Email = email, Password = password };

    private static async Task<TokenValidationResultLike> Read(string token)
    {
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, AuthFixture.ValidationParameters());
        return new TokenValidationResultLike(result.IsValid, result.ClaimsIdentity);
    }

    private sealed record TokenValidationResultLike(bool IsValid, System.Security.Claims.ClaimsIdentity Identity);

    [Fact]
    public async Task RightPassword_ReturnsASignedAccessToken_ThatBelongsToTheNewSession()
    {
        var user = _f.UserIs(_f.Staff());

        var response = await Handler().Handle(Login(), Ct);

        var token = await Read(response.AccessToken);
        token.IsValid.ShouldBeTrue();
        token.Identity.FindFirst("sub")!.Value.ShouldBe(user.Id.ToString());
        token.Identity.FindFirst("role")!.Value.ShouldBe("BankStaff");
        token.Identity.FindFirst("sid")!.Value.ShouldBe(_f.LastCreatedSession!.Id.ToString());
        response.ExpiresAt.ShouldBe(_f.Utc.AddMinutes(10));
        response.TokenType.ShouldBe("Bearer");
    }

    [Fact]
    public async Task Login_AlsoReturnsARefreshToken_ThatLivesThirtyMinutes_AndIsStoredOnlyAsAHash()
    {
        _f.UserIs(_f.Staff());

        var response = await Handler().Handle(Login(), Ct);

        response.RefreshToken.ShouldNotBeNullOrEmpty();
        response.RefreshTokenExpiresAt.ShouldBe(_f.Utc.AddMinutes(30));
        var session = _f.LastCreatedSession!;
        session.RefreshTokenHash.ShouldBe(SecureToken.Hash(response.RefreshToken));
        session.RefreshTokenHash.ShouldNotContain(response.RefreshToken);
        session.RefreshTokenExpiresAt.ShouldBe(_f.Utc.AddMinutes(30));
    }

    [Fact]
    public async Task EveryLogin_StartsItsOwnSession()
    {
        _f.UserIs(_f.Staff());

        var first = await Handler().Handle(Login(), Ct);
        var firstSession = _f.LastCreatedSession;
        var second = await Handler().Handle(Login(), Ct);

        _f.LastCreatedSession.ShouldNotBeSameAs(firstSession);
        first.RefreshToken.ShouldNotBe(second.RefreshToken);
        (await Read(first.AccessToken)).Identity.FindFirst("sid")!.Value
            .ShouldNotBe((await Read(second.AccessToken)).Identity.FindFirst("sid")!.Value);
    }

    [Fact]
    public async Task ForACustomer_TheCustomerIdIsTheUserId_AndNoSeparateClaimIsNeeded()
    {
        var customer = _f.UserIs(_f.Customer());

        var response = await Handler().Handle(Login(email: "customer@bank.test"), Ct);

        response.Role.ShouldBe(UserRole.Customer);
        response.CustomerId.ShouldBe(customer.Id);
        var token = await Read(response.AccessToken);
        token.Identity.FindFirst("role")!.Value.ShouldBe("Customer");
        token.Identity.FindFirst("sub")!.Value.ShouldBe(customer.Id.ToString());
        token.Identity.FindFirst("customerId").ShouldBeNull();
    }

    [Fact]
    public async Task ForStaff_ThereIsNoCustomerId()
    {
        _f.UserIs(_f.Staff());

        (await Handler().Handle(Login(), Ct)).CustomerId.ShouldBeNull();
    }

    [Fact]
    public async Task EmailIsMatchedTrimmedAndCaseInsensitive()
    {
        Expression<Func<ApplicationUser, bool>>? predicate = null;
        _f.Users.GetAsync(Arg.Do<Expression<Func<ApplicationUser, bool>>>(p => predicate = p),
                Arg.Any<Func<IQueryable<ApplicationUser>, IIncludableQueryable<ApplicationUser, object?>>?>(),
                Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(_f.Staff());

        await Handler().Handle(Login(email: "  STAFF@Bank.TEST  "), Ct);

        predicate!.Compile()(new ApplicationUser { Email = "staff@bank.test" }).ShouldBeTrue();
    }

    [Fact]
    public async Task UnknownEmail_FailsWithTheSameMessageAsAWrongPassword()
    {
        _f.UserIs(null);
        var unknown = await Should.ThrowAsync<AuthenticationException>(() => Handler().Handle(Login(), Ct));

        _f.UserIs(_f.Staff());
        var wrong = await Should.ThrowAsync<AuthenticationException>(() => Handler().Handle(Login("Wrong-Password-1"), Ct));

        unknown.Message.ShouldBe(wrong.Message);
    }

    [Fact]
    public async Task WrongPassword_CountsTheFailure_AndCreatesNoSession()
    {
        var user = _f.UserIs(_f.Staff());

        await Should.ThrowAsync<AuthenticationException>(() => Handler().Handle(Login("Wrong-Password-1"), Ct));

        user.FailedLoginCount.ShouldBe(1);
        await _f.Users.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        await _f.Sessions.DidNotReceiveWithAnyArgs().AddAsync(default!, Ct);
    }

    [Fact]
    public async Task FiveWrongPasswords_LockTheAccount_EvenForTheRightPassword()
    {
        var user = _f.UserIs(_f.Staff());

        for (int i = 0; i < AuthBusinessRules.MaxFailedLoginAttempts; i++)
            await Should.ThrowAsync<AuthenticationException>(() => Handler().Handle(Login("Wrong-Password-1"), Ct));

        user.LockoutEnd.ShouldBe(_f.Utc.Add(AuthBusinessRules.LockoutDuration));
        var locked = await Should.ThrowAsync<AuthenticationException>(() => Handler().Handle(Login(), Ct));
        locked.Message.ShouldContain("locked");
    }

    [Fact]
    public async Task AfterTheLockoutEnds_TheRightPasswordWorks_AndCountersReset()
    {
        var user = _f.UserIs(_f.Staff());
        for (int i = 0; i < AuthBusinessRules.MaxFailedLoginAttempts; i++)
            await Should.ThrowAsync<AuthenticationException>(() => Handler().Handle(Login("Wrong-Password-1"), Ct));

        _f.Time.Now = _f.Time.Now.Add(AuthBusinessRules.LockoutDuration).AddSeconds(1);
        var response = await Handler().Handle(Login(), Ct);

        response.AccessToken.ShouldNotBeNullOrEmpty();
        (user.FailedLoginCount, user.LockoutEnd).ShouldBe((0, null));
        user.LastLoginDate.ShouldBe(_f.Utc);
    }

    [Fact]
    public async Task SuccessResetsEarlierFailures()
    {
        var user = _f.UserIs(_f.Staff());
        user.FailedLoginCount = 4;

        await Handler().Handle(Login(), Ct);

        user.FailedLoginCount.ShouldBe(0);
    }

    [Fact]
    public async Task InactiveOrBannedUser_IsRefused_EvenWithTheRightPassword()
    {
        _f.UserIs(_f.Staff(active: false));

        var exception = await Should.ThrowAsync<AuthenticationException>(() => Handler().Handle(Login(), Ct));

        exception.Message.ShouldBe("Invalid e-mail or password.");
        await _f.Sessions.DidNotReceiveWithAnyArgs().AddAsync(default!, Ct);
    }

    [Fact]
    public async Task TheResponse_NeverContainsAPasswordHash()
    {
        _f.UserIs(_f.Staff());

        var response = await Handler().Handle(Login(), Ct);

        System.Text.Json.JsonSerializer.Serialize(response).ShouldNotContain("PBKDF2");
    }

    // ---- e-mail must be verified (off by default) ----

    [Fact]
    public async Task ByDefault_AnUnverifiedEmailCanStillLogin()
    {
        var user = _f.Staff(); user.EmailVerified = false;
        _f.UserIs(user);

        (await Handler().Handle(Login(), Ct)).AccessToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task WhenVerificationIsRequired_AnUnverifiedEmailCannotLogin_ButAVerifiedOneCan()
    {
        _f.EmailOptions.RequireVerifiedEmail = true;
        var user = _f.Staff(); user.EmailVerified = false;
        _f.UserIs(user);

        var refused = await Should.ThrowAsync<AuthenticationException>(() => Handler().Handle(Login(), Ct));
        refused.Message.ShouldContain("not verified");
        await _f.Sessions.DidNotReceiveWithAnyArgs().AddAsync(default!, Ct);

        user.EmailVerified = true;
        (await Handler().Handle(Login(), Ct)).AccessToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task TheVerificationCheck_DoesNotRevealAnythingToSomeoneWithTheWrongPassword()
    {
        _f.EmailOptions.RequireVerifiedEmail = true;
        var user = _f.Staff(); user.EmailVerified = false;
        _f.UserIs(user);

        var exception = await Should.ThrowAsync<AuthenticationException>(() => Handler().Handle(Login("Wrong-Password-1"), Ct));

        exception.Message.ShouldBe("Invalid e-mail or password.");
    }

    [Fact]
    public void Validator_ChecksEmailAndPasswordFormat()
    {
        var validator = new LoginCommandValidator();

        validator.TestValidate(Login()).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new LoginCommand { Email = "nope", Password = "x" }).ShouldHaveValidationErrorFor(c => c.Email);
        validator.TestValidate(new LoginCommand { Email = "a@b.com" }).ShouldHaveValidationErrorFor(c => c.Password);
    }
}

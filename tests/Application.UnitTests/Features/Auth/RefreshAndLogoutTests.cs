using System.Linq.Expressions;
using Application.Features.Auth.Commands.Login;
using Application.Features.Auth.Commands.Logout;
using Application.Features.Auth.Commands.Refresh;
using Application.UnitTests.Support;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using BankingSystemCore.Security.Tokens;
using Domain.Entities;
using FluentValidation.TestHelper;
using Microsoft.IdentityModel.JsonWebTokens;
using NSubstitute;

namespace Application.UnitTests.Features.Auth;

public class RefreshAndLogoutTests
{
    private readonly AuthFixture _f = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RefreshTokenCommandHandler Refresh() => new(_f.Sessions, _f.Users, _f.Issuer, _f.Time, _f.EmailOptionsAccessor, _f.Audit);

    private static string Sid(string accessToken) =>
        new JsonWebTokenHandler().ReadJsonWebToken(accessToken).GetClaim("sid").Value;

    /// <summary>Logs a staff user in and makes the fake repositories return the new session.</summary>
    private async Task<LoginResponse> LoginAsync(ApplicationUser? user = null)
    {
        user = _f.UserIs(user ?? _f.Staff());
        var login = await new LoginCommandHandler(_f.Users, _f.Hasher, _f.Issuer, _f.Time, _f.EmailOptionsAccessor, _f.Audit)
            .Handle(new LoginCommand { Email = user.Email, Password = AuthFixture.Password }, Ct);
        _f.SessionIs(_f.LastCreatedSession);
        return login;
    }

    [Fact]
    public async Task ARefreshToken_IsExchangedForANewPair_InTheSameSession()
    {
        var login = await LoginAsync();

        var refreshed = await Refresh().Handle(new RefreshTokenCommand { RefreshToken = login.RefreshToken }, Ct);

        refreshed.AccessToken.ShouldNotBe(login.AccessToken);
        refreshed.RefreshToken.ShouldNotBe(login.RefreshToken);
        Sid(refreshed.AccessToken).ShouldBe(Sid(login.AccessToken)); // same session
        refreshed.Email.ShouldBe(login.Email);
    }

    [Fact]
    public async Task Refreshing_StoresOnlyTheHashOfTheNewToken_AndRemembersTheOldOne()
    {
        var login = await LoginAsync();
        var session = _f.LastCreatedSession!;
        _f.Time.Now = _f.Time.Now.AddMinutes(5);

        var refreshed = await Refresh().Handle(new RefreshTokenCommand { RefreshToken = login.RefreshToken }, Ct);

        session.RefreshTokenHash.ShouldBe(SecureToken.Hash(refreshed.RefreshToken));
        session.PreviousRefreshTokenHash.ShouldBe(SecureToken.Hash(login.RefreshToken));
        session.LastRefreshDate.ShouldBe(_f.Utc);
        await _f.Sessions.Received().UpdateAsync(session, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EveryRefresh_ExtendsTheLifetime_ByThirtyMinutes_SoAnActiveSessionKeepsGoing()
    {
        var login = await LoginAsync();
        _f.Time.Now = _f.Time.Now.AddMinutes(25); // still inside the first 30 minutes

        var refreshed = await Refresh().Handle(new RefreshTokenCommand { RefreshToken = login.RefreshToken }, Ct);

        refreshed.RefreshTokenExpiresAt.ShouldBe(_f.Utc.AddMinutes(30));
        refreshed.RefreshTokenExpiresAt.ShouldBeGreaterThan(login.RefreshTokenExpiresAt);

        _f.Time.Now = _f.Time.Now.AddMinutes(25); // 50 minutes after login, but 25 after the last refresh
        (await Refresh().Handle(new RefreshTokenCommand { RefreshToken = refreshed.RefreshToken }, Ct)).AccessToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ARefreshToken_WorksOnce_UsingTheOldOneAgainEndsTheWholeSession()
    {
        var login = await LoginAsync();
        var session = _f.LastCreatedSession!;
        var refreshed = await Refresh().Handle(new RefreshTokenCommand { RefreshToken = login.RefreshToken }, Ct);

        // Somebody presents the token that was already replaced: it was copied.
        await Should.ThrowAsync<AuthenticationException>(() => Refresh().Handle(new RefreshTokenCommand { RefreshToken = login.RefreshToken }, Ct));

        session.RevokedDate.ShouldBe(_f.Utc);
        session.RevokeReason!.ShouldContain("reuse");

        // Both the thief and the real user are out: even the newest token is dead now.
        await Should.ThrowAsync<AuthenticationException>(() => Refresh().Handle(new RefreshTokenCommand { RefreshToken = refreshed.RefreshToken }, Ct));
    }

    [Fact]
    public async Task UnknownToken_IsRefused()
    {
        _f.SessionIs(null);

        await Should.ThrowAsync<AuthenticationException>(() => Refresh().Handle(new RefreshTokenCommand { RefreshToken = "made-up" }, Ct));
    }

    [Fact]
    public async Task AnExpiredToken_IsRefused_AndEndsTheSession()
    {
        var login = await LoginAsync();
        var session = _f.LastCreatedSession!;
        _f.Time.Now = _f.Time.Now.AddMinutes(31);

        await Should.ThrowAsync<AuthenticationException>(() => Refresh().Handle(new RefreshTokenCommand { RefreshToken = login.RefreshToken }, Ct));

        session.RevokedDate.ShouldNotBeNull();
        session.RevokeReason!.ShouldContain("expired");
    }

    [Fact]
    public async Task AToken_IsStillAcceptedJustBeforeItExpires()
    {
        var login = await LoginAsync();
        _f.Time.Now = _f.Time.Now.AddMinutes(29).AddSeconds(59);

        (await Refresh().Handle(new RefreshTokenCommand { RefreshToken = login.RefreshToken }, Ct)).AccessToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ARevokedSession_CannotBeRefreshed()
    {
        var login = await LoginAsync();
        _f.LastCreatedSession!.RevokedDate = _f.Utc;

        await Should.ThrowAsync<AuthenticationException>(() => Refresh().Handle(new RefreshTokenCommand { RefreshToken = login.RefreshToken }, Ct));
    }

    [Fact]
    public async Task ABannedOrDeactivatedUser_CannotRefresh_AndTheSessionEnds()
    {
        var login = await LoginAsync();
        var session = _f.LastCreatedSession!;
        _f.UserIs(_f.Staff(active: false));

        await Should.ThrowAsync<AuthenticationException>(() => Refresh().Handle(new RefreshTokenCommand { RefreshToken = login.RefreshToken }, Ct));

        session.RevokedDate.ShouldNotBeNull();
    }

    [Fact]
    public async Task WhenVerificationIsRequired_AnUnverifiedUser_CannotRefresh()
    {
        var login = await LoginAsync();
        _f.EmailOptions.RequireVerifiedEmail = true;
        var unverified = _f.Staff(); unverified.EmailVerified = false;
        _f.UserIs(unverified);

        await Should.ThrowAsync<AuthenticationException>(() => Refresh().Handle(new RefreshTokenCommand { RefreshToken = login.RefreshToken }, Ct));
    }

    [Fact]
    public async Task Refreshing_NeverWritesTheUser_SoItCannotUndoABan()
    {
        var login = await LoginAsync();
        _f.Users.ClearReceivedCalls();

        await Refresh().Handle(new RefreshTokenCommand { RefreshToken = login.RefreshToken }, Ct);

        await _f.Users.DidNotReceiveWithAnyArgs().UpdateAsync(default!, Ct);
        await _f.Users.Received().GetAsync(
            Arg.Any<Expression<Func<ApplicationUser, bool>>>(), Arg.Any<Func<IQueryable<ApplicationUser>, Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<ApplicationUser, object?>>?>(),
            Arg.Any<bool>(), false, Arg.Any<CancellationToken>()); // enableTracking = false
    }

    [Fact]
    public async Task TheLookup_FindsTheSessionByTheCurrentOrThePreviousToken()
    {
        Expression<Func<UserSession, bool>>? predicate = null;
        _f.Sessions.GetAsync(Arg.Do<Expression<Func<UserSession, bool>>>(p => predicate = p),
                Arg.Any<Func<IQueryable<UserSession>, Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<UserSession, object?>>?>(),
                Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((UserSession?)null);

        await Should.ThrowAsync<AuthenticationException>(() => Refresh().Handle(new RefreshTokenCommand { RefreshToken = "token-a" }, Ct));

        var session = new UserSession { RefreshTokenHash = SecureToken.Hash("token-a"), PreviousRefreshTokenHash = SecureToken.Hash("token-b") };
        var matches = predicate!.Compile();
        matches(session).ShouldBeTrue();
        matches(new UserSession { RefreshTokenHash = SecureToken.Hash("x"), PreviousRefreshTokenHash = SecureToken.Hash("token-a") }).ShouldBeTrue();
        matches(new UserSession { RefreshTokenHash = SecureToken.Hash("x"), PreviousRefreshTokenHash = SecureToken.Hash("y") }).ShouldBeFalse();
    }

    [Fact]
    public void RefreshValidator_RequiresAToken()
    {
        var validator = new RefreshTokenCommandValidator();

        validator.TestValidate(new RefreshTokenCommand { RefreshToken = "abc" }).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new RefreshTokenCommand()).ShouldHaveValidationErrorFor(c => c.RefreshToken);
        validator.TestValidate(new RefreshTokenCommand { RefreshToken = new string('x', 201) }).ShouldHaveValidationErrorFor(c => c.RefreshToken);
    }

    // ---- logout ----

    [Fact]
    public async Task Logout_EndsTheSessionOfTheCurrentToken()
    {
        var session = new UserSession { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        _f.SessionIs(session);
        var handler = new LogoutCommandHandler(FakeCurrentUser.Staff(), _f.Sessions, _f.Time);

        await handler.Handle(new LogoutCommand(), Ct);

        session.RevokedDate.ShouldBe(_f.Utc);
        session.RevokeReason.ShouldBe("Logout");
        await _f.Sessions.Received(1).UpdateAsync(session, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Logout_WithATokenWithoutASession_DoesNothing()
    {
        var handler = new LogoutCommandHandler(FakeCurrentUser.CustomerWithoutId(), _f.Sessions, _f.Time);

        await handler.Handle(new LogoutCommand(), Ct);

        await _f.Sessions.DidNotReceiveWithAnyArgs().UpdateAsync(default!, Ct);
    }

    [Fact]
    public async Task Logout_Twice_IsHarmless()
    {
        _f.SessionIs(null); // already ended: the lookup only finds live sessions
        var handler = new LogoutCommandHandler(FakeCurrentUser.Staff(), _f.Sessions, _f.Time);

        await Should.NotThrowAsync(() => handler.Handle(new LogoutCommand(), Ct));

        await _f.Sessions.DidNotReceiveWithAnyArgs().UpdateAsync(default!, Ct);
    }

    [Fact]
    public async Task AfterLogout_TheRefreshTokenOfThatSessionIsDead()
    {
        var login = await LoginAsync();
        var session = _f.LastCreatedSession!;
        await new LogoutCommandHandler(FakeCurrentUser.Staff(), _f.Sessions, _f.Time).Handle(new LogoutCommand(), Ct);

        session.RevokedDate.ShouldNotBeNull();
        await Should.ThrowAsync<AuthenticationException>(() => Refresh().Handle(new RefreshTokenCommand { RefreshToken = login.RefreshToken }, Ct));
    }
}

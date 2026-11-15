using Application.Features.Auth.Commands.Login;
using Application.Features.Auth.Commands.Logout;
using Application.Features.Auth.Commands.Refresh;
using Application.Features.Auth.Commands.SendEmailVerification;
using Application.Features.Auth.Commands.VerifyEmail;
using Application.Features.IndividualCustomers.Commands.Delete;
using Application.Features.Users.Commands.Ban;
using Application.Features.Users.Commands.Unban;
using Application.Features.Users.Queries.GetList;
using Application.Services.Security;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Persistence.IntegrationTests.Infrastructure;

namespace Persistence.IntegrationTests.Security;

/// <summary>Sessions, refresh tokens, bans and e-mail verification, tried against a real database.</summary>
[Collection(DatabaseCollection.Name)]
public class SessionBanAndVerificationFlowTests(SqlServerFixture database) : IntegrationTestBase(database)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Guid> NewCustomerAsync(int seed = 1)
    {
        CurrentUser.AsStaff();
        Guid id = (await Send(TestData.IndividualCreate(seed))).Id;
        CurrentUser.AsAnonymous();
        return id;
    }

    private Task<LoginResponse> LoginAsync(int seed = 1) =>
        Send(new LoginCommand { Email = TestData.Email(seed), Password = TestData.Password });

    private async Task<bool> SessionIsAlive(Guid sessionId)
    {
        using var scope = NewScope();
        return await Resolve<ISessionValidator>(scope).IsActiveAsync(sessionId, Ct);
    }

    private static Guid SessionOf(string accessToken) =>
        Guid.Parse(new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(accessToken).GetClaim("sid").Value);

    // ---- sessions ----

    [Fact]
    public async Task EveryLogin_StartsASession_ThatIsAliveUntilItEnds()
    {
        await NewCustomerAsync();

        var first = await LoginAsync();
        var second = await LoginAsync();

        SessionOf(first.AccessToken).ShouldNotBe(SessionOf(second.AccessToken));
        (await SessionIsAlive(SessionOf(first.AccessToken))).ShouldBeTrue();
        (await SessionIsAlive(SessionOf(second.AccessToken))).ShouldBeTrue();
        (await UsingDbContext(db => db.UserSessions.CountAsync(Ct))).ShouldBe(2);
    }

    // ---- refresh tokens ----

    [Fact]
    public async Task Refresh_GivesANewPair_ForTheSameSession_AndTheOldRefreshTokenStopsWorking()
    {
        await NewCustomerAsync();
        var login = await LoginAsync();

        var refreshed = await Send(new RefreshTokenCommand { RefreshToken = login.RefreshToken });

        refreshed.RefreshToken.ShouldNotBe(login.RefreshToken);
        SessionOf(refreshed.AccessToken).ShouldBe(SessionOf(login.AccessToken));
        (await SessionIsAlive(SessionOf(refreshed.AccessToken))).ShouldBeTrue();
        (await UsingDbContext(db => db.UserSessions.CountAsync(Ct))).ShouldBe(1); // rotated, not duplicated
    }

    [Fact]
    public async Task UsingAnOldRefreshToken_EndsTheWholeSession_AlsoForTheNewestToken_AndForTheAccessToken()
    {
        await NewCustomerAsync();
        var login = await LoginAsync();
        var refreshed = await Send(new RefreshTokenCommand { RefreshToken = login.RefreshToken });

        await Should.ThrowAsync<AuthenticationException>(() => Send(new RefreshTokenCommand { RefreshToken = login.RefreshToken }));

        await Should.ThrowAsync<AuthenticationException>(() => Send(new RefreshTokenCommand { RefreshToken = refreshed.RefreshToken }));
        (await SessionIsAlive(SessionOf(refreshed.AccessToken))).ShouldBeFalse();
        var session = await UsingDbContext(db => Task.FromResult(db.UserSessions.AsNoTracking().Single()));
        session.RevokeReason.ShouldNotBeNull().ShouldContain("reuse");
    }

    [Fact]
    public async Task TheRefreshToken_IsStoredOnlyAsAHash()
    {
        await NewCustomerAsync();
        var login = await LoginAsync();

        var session = await UsingDbContext(db => Task.FromResult(db.UserSessions.AsNoTracking().Single()));

        session.RefreshTokenHash.ShouldNotContain(login.RefreshToken);
        session.RefreshTokenHash.Length.ShouldBe(64);
    }

    [Fact]
    public async Task TwoRefreshesWithTheSameToken_AtTheSameTime_CannotBothWin()
    {
        await NewCustomerAsync();
        var login = await LoginAsync();

        // Two requests read the session before either one saves.
        using var scopeA = ScopedDb(out var dbA);
        using var scopeB = ScopedDb(out var dbB);
        var sessionA = await dbA.UserSessions.SingleAsync(Ct);
        var sessionB = await dbB.UserSessions.SingleAsync(Ct);

        sessionA.RefreshTokenHash = new string('A', 64);
        await dbA.SaveChangesAsync(Ct);
        sessionB.RefreshTokenHash = new string('B', 64);

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync(Ct));
        login.RefreshToken.ShouldNotBeNull();
    }

    // ---- ban: instant ----

    [Fact]
    public async Task Ban_IsStoredOnTheApplicationUser_WithDateAndReason()
    {
        Guid customerId = await NewCustomerAsync();
        CurrentUser.AsStaff();

        await Send(new BanUserCommand { Id = customerId, Reason = "Fraud" });

        var user = await UsingDbContext(db => Task.FromResult(db.ApplicationUsers.AsNoTracking().Single()));
        (user.IsActive, user.BanReason, user.BannedDate is not null).ShouldBe((false, "Fraud", true));
    }

    // ---- deleting a customer ----

    [Fact]
    public async Task ADeletedCustomersEmail_CannotBeUsedAgain()
    {
        Guid customerId = await NewCustomerAsync();
        CurrentUser.AsStaff();
        await Send(new DeleteIndividualCustomerCommand { Id = customerId });

        var again = TestData.IndividualCreate(2);
        again.Email = TestData.Email(1);

        await Should.ThrowAsync<BusinessException>(() => Send(again));
    }

    // ---- e-mail verification ----

    [Fact]
    public async Task ANewCustomer_GetsAVerificationEmail_WhoseTokenVerifiesTheAddress_Once()
    {
        await NewCustomerAsync();
        Emails.Sent.ShouldHaveSingleItem().To.ShouldBe(TestData.Email(1));
        string token = Emails.TokenSentTo(TestData.Email(1));

        var verified = await Send(new VerifyEmailCommand { Email = TestData.Email(1), Token = token });

        verified.EmailVerified.ShouldBeTrue();
        var user = await UsingDbContext(db => Task.FromResult(db.ApplicationUsers.AsNoTracking().Single()));
        (user.EmailVerified, user.EmailVerifiedDate is not null, user.EmailVerificationTokenHash).ShouldBe((true, true, null));
        await Should.ThrowAsync<BusinessException>(() => Send(new VerifyEmailCommand { Email = TestData.Email(1), Token = token }));
    }

    [Fact]
    public async Task TheVerificationToken_IsStoredOnlyAsAHash_AndExpires()
    {
        await NewCustomerAsync();
        string token = Emails.TokenSentTo(TestData.Email(1));
        var user = await UsingDbContext(db => Task.FromResult(db.ApplicationUsers.AsNoTracking().Single()));

        user.EmailVerificationTokenHash!.ShouldNotContain(token);

        await UsingDbContext(async db =>
        {
            db.ApplicationUsers.Single().EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            return await db.SaveChangesAsync(Ct);
        });
        await Should.ThrowAsync<BusinessException>(() => Send(new VerifyEmailCommand { Email = TestData.Email(1), Token = token }));
    }
}

/// <summary>The same application with "RequireVerifiedEmail" switched on.</summary>
[Collection(DatabaseCollection.Name)]
public class RequiredEmailVerificationTests(SqlServerFixture database)
    : IntegrationTestBase(database, new() { ["Security:EmailVerification:RequireVerifiedEmail"] = "true" })
{
    [Fact]
    public async Task AnUnverifiedCustomer_CannotLogin_UntilTheAddressIsVerified()
    {
        CurrentUser.AsStaff();
        await Send(TestData.IndividualCreate());
        CurrentUser.AsAnonymous();
        var login = new LoginCommand { Email = TestData.Email(1), Password = TestData.Password };

        var refused = await Should.ThrowAsync<AuthenticationException>(() => Send(login));
        refused.Message.ShouldContain("not verified");
        (await UsingDbContext(db => db.UserSessions.CountAsync(TestContext.Current.CancellationToken))).ShouldBe(0);

        await Send(new VerifyEmailCommand { Email = TestData.Email(1), Token = Emails.TokenSentTo(TestData.Email(1)) });

        (await Send(login)).AccessToken.ShouldNotBeNullOrEmpty();
    }
}

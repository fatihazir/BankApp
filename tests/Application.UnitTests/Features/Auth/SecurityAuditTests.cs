using Application.Features.Auth.Commands.Login;
using Application.Features.Auth.Commands.Refresh;
using Application.Features.Auth.Rules;
using Application.Services.Auditing;
using Application.UnitTests.Support;
using BankingSystemCore.Application.Pipelines.Auditing;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Application.UnitTests.Features.Auth;

/// <summary>Logins and refused logins leave a trace in the audit trail, and never the password.</summary>
public class SecurityAuditTests
{
    private readonly AuthFixture _f = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private LoginCommandHandler Login() => new(_f.Users, _f.Hasher, _f.Issuer, _f.Time, _f.EmailOptionsAccessor, _f.Audit);

    private RefreshTokenCommandHandler Refresh() => new(_f.Sessions, _f.Users, _f.Issuer, _f.Time, _f.EmailOptionsAccessor, _f.Audit);

    private static LoginCommand Command(string password = AuthFixture.Password) => new() { Email = "staff@bank.test", Password = password };

    private IEnumerable<AuditEntry> Written() =>
        _f.AuditWriter.ReceivedCalls().Select(c => (AuditEntry)c.GetArguments()[0]!);

    [Fact]
    public async Task ASuccessfulLogin_IsRecorded_WithWhoAndFromWhere()
    {
        var user = _f.UserIs(_f.Staff());

        await Login().Handle(Command(), Ct);

        var entry = Written().ShouldHaveSingleItem();
        (entry.Action, entry.Succeeded, entry.ActorUserId, entry.ActorEmail, entry.ActorRole, entry.TargetId)
            .ShouldBe(("Auth.Login.Succeeded", true, user.Id, "staff@bank.test", "BankStaff", user.Id));
    }

    [Fact]
    public async Task AWrongPassword_IsRecorded_ButNeverTheAttemptedPassword()
    {
        _f.UserIs(_f.Staff());

        await Should.ThrowAsync<AuthenticationException>(() => Login().Handle(Command("Wrong-Password-1"), Ct));

        var entry = Written().ShouldHaveSingleItem();
        (entry.Action, entry.Succeeded, entry.Detail).ShouldBe(("Auth.Login.Failed", false, "InvalidPassword"));
        System.Text.Json.JsonSerializer.Serialize(entry).ShouldNotContain("Wrong-Password-1");
    }

    [Fact]
    public async Task ABannedUser_IsRecordedWithTheReasonAccountInactive()
    {
        _f.UserIs(_f.Staff(active: false));

        await Should.ThrowAsync<AuthenticationException>(() => Login().Handle(Command(), Ct));

        Written().ShouldHaveSingleItem().Detail.ShouldBe("AccountInactive");
    }

    [Fact]
    public async Task TheLockout_IsRecordedOnce_AtTheFifthFailure()
    {
        _f.UserIs(_f.Staff());

        for (int i = 0; i < AuthBusinessRules.MaxFailedLoginAttempts; i++)
            await Should.ThrowAsync<AuthenticationException>(() => Login().Handle(Command("Wrong-Password-1"), Ct));
        // While the account is locked nothing more is stored: anybody can send those.
        await Should.ThrowAsync<AuthenticationException>(() => Login().Handle(Command("Wrong-Password-1"), Ct));
        await Should.ThrowAsync<AuthenticationException>(() => Login().Handle(Command(), Ct));

        var entries = Written().ToList();
        entries.Count(e => e.Action == "Auth.Login.Failed").ShouldBe(AuthBusinessRules.MaxFailedLoginAttempts);
        entries.Count(e => e.Action == "Auth.Login.LockedOut").ShouldBe(1);
        entries.Count.ShouldBe(AuthBusinessRules.MaxFailedLoginAttempts + 1);
    }

    [Fact]
    public async Task AnUnknownEmail_IsNotStored_BecauseAnybodyCanSendIt()
    {
        _f.UserIs(null);

        await Should.ThrowAsync<AuthenticationException>(() => Login().Handle(Command(), Ct));

        _f.AuditWriter.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task AReusedRefreshToken_IsRecordedAsASecurityEvent()
    {
        var user = _f.UserIs(_f.Staff());
        var login = await Login().Handle(Command(), Ct);
        var session = _f.SessionIs(_f.LastCreatedSession);
        await Refresh().Handle(new RefreshTokenCommand { RefreshToken = login.RefreshToken }, Ct);
        _f.AuditWriter.ClearReceivedCalls();

        await Should.ThrowAsync<AuthenticationException>(() => Refresh().Handle(new RefreshTokenCommand { RefreshToken = login.RefreshToken }, Ct));

        var entry = Written().ShouldHaveSingleItem();
        (entry.Action, entry.Succeeded, entry.ActorUserId).ShouldBe(("Auth.Refresh.ReuseDetected", false, user.Id));
        entry.Detail.ShouldNotBeNull().ShouldContain(session.Id.ToString());
    }

    [Fact]
    public async Task AFailingAuditStore_NeverBreaksTheLogin()
    {
        _f.UserIs(_f.Staff());
        _f.AuditWriter.WriteAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("table is gone"));

        var response = await Login().Handle(Command(), Ct);

        response.AccessToken.ShouldNotBeNullOrEmpty();
    }
}

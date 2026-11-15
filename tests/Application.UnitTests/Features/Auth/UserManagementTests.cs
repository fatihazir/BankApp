using System.Linq.Expressions;
using Application.Features.Users.Commands.Ban;
using Application.Features.Users.Commands.CreateStaff;
using Application.Features.Users.Commands.Unban;
using Application.Features.Users.Queries.GetList;
using Application.Services.Security;
using Application.UnitTests.Support;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using BankingSystemCore.Repositories;
using Domain.Entities;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore.Query;
using NSubstitute;

namespace Application.UnitTests.Features.Auth;

public class UserManagementTests
{
    private readonly AuthFixture _f = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private UserSessionService SessionService() => new(_f.Sessions, _f.Time);

    private BanUserCommandHandler Ban(FakeCurrentUser current) => new(_f.Users, SessionService(), current, _f.Time);

    private void SessionsAre(params IEnumerable<UserSession>[] batches) =>
        _f.Sessions.GetListAsync(
                Arg.Any<Expression<Func<UserSession, bool>>?>(),
                Arg.Any<Func<IQueryable<UserSession>, IOrderedQueryable<UserSession>>?>(),
                Arg.Any<Func<IQueryable<UserSession>, IIncludableQueryable<UserSession, object?>>?>(),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(batches.Select(b => new Paginate<UserSession> { Items = b.ToList() }).First(),
                     batches.Skip(1).Select(b => new Paginate<UserSession> { Items = b.ToList() }).ToArray());

    private static List<UserSession> LiveSessions(int count) =>
        Enumerable.Range(0, count).Select(_ => new UserSession { Id = Guid.NewGuid() }).ToList();

    // ---- ban ----

    [Fact]
    public async Task Ban_BlocksTheUser_AndEndsEverySession()
    {
        var user = _f.UserIs(_f.Customer());
        var sessions = LiveSessions(3);
        SessionsAre(sessions);

        var result = await Ban(FakeCurrentUser.Staff()).Handle(new BanUserCommand { Id = user.Id, Reason = "  Fraud suspected  " }, Ct);

        (user.IsActive, user.BannedDate, user.BanReason).ShouldBe((false, _f.Utc, "Fraud suspected"));
        sessions.ShouldAllBe(s => s.RevokedDate == _f.Utc && s.RevokeReason == "User banned");
        (result.RevokedSessions, result.IsActive, result.BanReason).ShouldBe((3, false, "Fraud suspected"));
    }

    [Fact]
    public async Task Ban_FirstBlocksTheUser_ThenEndsTheSessions()
    {
        var user = _f.UserIs(_f.Customer());
        SessionsAre(LiveSessions(1));

        await Ban(FakeCurrentUser.Staff()).Handle(new BanUserCommand { Id = user.Id, Reason = "x" }, Ct);

        // The user is saved as inactive before any session is touched, so no request slips through in between.
        Received.InOrder(() =>
        {
            _f.Users.UpdateAsync(user, Arg.Any<CancellationToken>());
            _f.Sessions.UpdateRangeAsync(Arg.Any<ICollection<UserSession>>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Ban_CannotBeAppliedToYourself()
    {
        var staff = _f.UserIs(_f.Staff());

        await Should.ThrowAsync<BusinessException>(
            () => Ban(FakeCurrentUserWithId(staff.Id)).Handle(new BanUserCommand { Id = staff.Id, Reason = "x" }, Ct));

        staff.IsActive.ShouldBeTrue();
        await _f.Users.DidNotReceiveWithAnyArgs().UpdateAsync(default!, Ct);
    }

    private static FakeCurrentUser FakeCurrentUserWithId(Guid id) => FakeCurrentUser.Customer(id); // only the id matters here

    [Fact]
    public async Task Ban_AnAlreadyBannedUser_IsRefused()
    {
        var user = _f.UserIs(_f.Customer());
        user.BannedDate = _f.Utc;

        await Should.ThrowAsync<BusinessException>(() => Ban(FakeCurrentUser.Staff()).Handle(new BanUserCommand { Id = user.Id, Reason = "x" }, Ct));
    }

    [Fact]
    public async Task Ban_AnUnknownUser_IsNotFound()
    {
        _f.UserIs(null);

        await Should.ThrowAsync<NotFoundException>(() => Ban(FakeCurrentUser.Staff()).Handle(new BanUserCommand { Id = Guid.NewGuid(), Reason = "x" }, Ct));
    }

    [Fact]
    public async Task Ban_ANewlyBannedUserWithoutSessions_Works()
    {
        var user = _f.UserIs(_f.Customer());
        SessionsAre(new List<UserSession>());

        var result = await Ban(FakeCurrentUser.Staff()).Handle(new BanUserCommand { Id = user.Id, Reason = "x" }, Ct);

        result.RevokedSessions.ShouldBe(0);
        await _f.Sessions.DidNotReceiveWithAnyArgs().UpdateRangeAsync(default!, Ct);
    }

    [Fact]
    public void BanValidator_NeedsAnIdAndAReason()
    {
        var validator = new BanUserCommandValidator();

        validator.TestValidate(new BanUserCommand { Id = Guid.NewGuid(), Reason = "x" }).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new BanUserCommand { Reason = "x" }).ShouldHaveValidationErrorFor(c => c.Id);
        validator.TestValidate(new BanUserCommand { Id = Guid.NewGuid(), Reason = " " }).ShouldHaveValidationErrorFor(c => c.Reason);
        validator.TestValidate(new BanUserCommand { Id = Guid.NewGuid(), Reason = new string('x', 501) }).ShouldHaveValidationErrorFor(c => c.Reason);
    }

    // ---- unban ----

    [Fact]
    public async Task Unban_ReactivatesTheUser_AndClearsTheLockout()
    {
        var user = _f.UserIs(_f.Customer());
        (user.IsActive, user.BannedDate, user.BanReason, user.FailedLoginCount, user.LockoutEnd) = (false, _f.Utc, "x", 3, _f.Utc.AddMinutes(5));

        var result = await new UnbanUserCommandHandler(_f.Users).Handle(new UnbanUserCommand { Id = user.Id }, Ct);

        result.IsActive.ShouldBeTrue();
        (user.IsActive, user.BannedDate, user.BanReason, user.FailedLoginCount, user.LockoutEnd).ShouldBe((true, null, null, 0, null));
        await _f.Sessions.DidNotReceiveWithAnyArgs().UpdateRangeAsync(default!, Ct); // old sessions stay dead
    }

    [Fact]
    public async Task Unban_AUserWhoIsNotBanned_IsRefused()
    {
        var user = _f.UserIs(_f.Customer());

        await Should.ThrowAsync<BusinessException>(() => new UnbanUserCommandHandler(_f.Users).Handle(new UnbanUserCommand { Id = user.Id }, Ct));
    }

    [Fact]
    public async Task Unban_AnUnknownUser_IsNotFound()
    {
        _f.UserIs(null);

        await Should.ThrowAsync<NotFoundException>(() => new UnbanUserCommandHandler(_f.Users).Handle(new UnbanUserCommand { Id = Guid.NewGuid() }, Ct));
    }

    // ---- ending sessions in bulk ----

    [Fact]
    public async Task RevokeAll_WorksInBatches_UntilNoLiveSessionIsLeft()
    {
        var first = LiveSessions(200);
        var second = LiveSessions(50);
        SessionsAre(first, second);

        int revoked = await SessionService().RevokeAllAsync(Guid.NewGuid(), "reason", Ct);

        revoked.ShouldBe(250);
        first.Concat(second).ShouldAllBe(s => s.RevokedDate == _f.Utc && s.RevokeReason == "reason");
        await _f.Sessions.Received(2).UpdateRangeAsync(Arg.Any<ICollection<UserSession>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeAll_OnlyLooksForLiveSessionsOfThatUser()
    {
        Expression<Func<UserSession, bool>>? predicate = null;
        _f.Sessions.GetListAsync(
                Arg.Do<Expression<Func<UserSession, bool>>?>(p => predicate = p),
                Arg.Any<Func<IQueryable<UserSession>, IOrderedQueryable<UserSession>>?>(),
                Arg.Any<Func<IQueryable<UserSession>, IIncludableQueryable<UserSession, object?>>?>(),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new Paginate<UserSession>());
        Guid userId = Guid.NewGuid();

        await SessionService().RevokeAllAsync(userId, "x", Ct);

        var matches = predicate!.Compile();
        matches(new UserSession { UserId = userId }).ShouldBeTrue();
        matches(new UserSession { UserId = userId, RevokedDate = _f.Utc }).ShouldBeFalse();
        matches(new UserSession { UserId = Guid.NewGuid() }).ShouldBeFalse();
    }

    // ---- is a session still alive? (asked on every request) ----

    [Fact]
    public async Task TheSessionCheck_AcceptsOnlyLiveSessionsOfActiveUsers()
    {
        Expression<Func<UserSession, bool>>? predicate = null;
        _f.Sessions.AnyAsync(Arg.Do<Expression<Func<UserSession, bool>>?>(p => predicate = p), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(true);
        Guid id = Guid.NewGuid();

        await new SessionValidator(_f.Sessions).IsActiveAsync(id, Ct);

        var matches = predicate!.Compile();
        UserSession Session(Action<UserSession>? change = null, Action<ApplicationUser>? changeUser = null)
        {
            var user = new ApplicationUser { IsActive = true };
            changeUser?.Invoke(user);
            var session = new UserSession { Id = id, User = user };
            change?.Invoke(session);
            return session;
        }

        matches(Session()).ShouldBeTrue();
        matches(Session(s => s.RevokedDate = _f.Utc)).ShouldBeFalse();                       // logged out / banned
        matches(Session(changeUser: u => u.IsActive = false)).ShouldBeFalse();               // banned or deactivated
        matches(Session(changeUser: u => u.DeleteDate = _f.Utc)).ShouldBeFalse();            // deleted
        matches(Session(s => s.Id = Guid.NewGuid())).ShouldBeFalse();                        // another session
    }

    // ---- staff accounts and the list ----

    [Fact]
    public async Task CreateStaff_CreatesAVerifiedStaffUser_WithAHashedPassword()
    {
        var handler = new CreateStaffUserCommandHandler(_f.Users, _f.Hasher, new Application.Features.Auth.Rules.AuthBusinessRules(_f.Users), _f.Time);

        var response = await handler.Handle(new CreateStaffUserCommand { Email = " New.Staff@Bank.test ", Password = AuthFixture.Password }, Ct);

        response.Role.ShouldBe(UserRole.BankStaff);
        response.Email.ShouldBe("new.staff@bank.test");
        await _f.Users.Received(1).AddAsync(
            Arg.Is<ApplicationUser>(u => u.Role == UserRole.BankStaff && u.IsActive && u.EmailVerified
                                         && u.PasswordHash != AuthFixture.Password && _f.Hasher.Verify(AuthFixture.Password, u.PasswordHash)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateStaff_WithARegisteredEmail_IsRefused()
    {
        _f.Users.AnyAsync(Arg.Any<Expression<Func<ApplicationUser, bool>>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(true);
        var handler = new CreateStaffUserCommandHandler(_f.Users, _f.Hasher, new Application.Features.Auth.Rules.AuthBusinessRules(_f.Users), _f.Time);

        await Should.ThrowAsync<BusinessException>(() => handler.Handle(new CreateStaffUserCommand { Email = "a@b.com", Password = AuthFixture.Password }, Ct));
        await _f.Users.DidNotReceiveWithAnyArgs().AddAsync(default!, Ct);
    }

    [Fact]
    public void CreateStaffValidator_EnforcesAStrongPassword()
    {
        var validator = new CreateStaffUserCommandValidator();

        validator.TestValidate(new CreateStaffUserCommand { Email = "a@b.com", Password = AuthFixture.Password }).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new CreateStaffUserCommand { Email = "a@b.com", Password = "weak" }).ShouldHaveValidationErrorFor(c => c.Password);
        validator.TestValidate(new CreateStaffUserCommand { Email = "nope", Password = AuthFixture.Password }).ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Fact]
    public async Task TheUserList_FillsTheCustomerFields_OnlyForCustomers()
    {
        var customer = new IndividualCustomer { Id = Guid.NewGuid(), Email = "c@bank.test", CustomerNumber = "IND-1" };
        var staff = _f.Staff();
        _f.Users.GetListAsync(
                Arg.Any<Expression<Func<ApplicationUser, bool>>?>(),
                Arg.Any<Func<IQueryable<ApplicationUser>, IOrderedQueryable<ApplicationUser>>?>(),
                Arg.Any<Func<IQueryable<ApplicationUser>, IIncludableQueryable<ApplicationUser, object?>>?>(),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new Paginate<ApplicationUser> { Index = 0, Size = 10, Count = 2, Pages = 1, Items = [customer, staff] });

        var page = await new GetListUserQueryHandler(_f.Users).Handle(new GetListUserQuery(), Ct);

        page.Items[0].CustomerId.ShouldBe(customer.Id);
        page.Items[0].CustomerNumber.ShouldBe("IND-1");
        page.Items[0].Role.ShouldBe(UserRole.Customer);
        page.Items[1].CustomerId.ShouldBeNull();
        page.Items[1].CustomerNumber.ShouldBeNull();
        page.Items[1].Role.ShouldBe(UserRole.BankStaff);
        page.Count.ShouldBe(2);
    }

    [Fact]
    public void TheUserList_NeverExposesSecrets()
    {
        var names = typeof(GetListUserListItemDto).GetProperties().Select(p => p.Name).ToList();

        names.ShouldNotContain(n => n.Contains("Password") || n.Contains("Hash") || n.Contains("RowVersion") || n.Contains("Token"));
    }

    [Theory]
    [InlineData(-1, 10, false)]
    [InlineData(0, 0, false)]
    [InlineData(0, 101, false)]
    [InlineData(0, 100, true)]
    public void TheUserListValidator_ChecksPaging(int index, int size, bool valid)
    {
        var result = new GetListUserQueryValidator().TestValidate(new GetListUserQuery { Index = index, Size = size });

        result.IsValid.ShouldBe(valid);
    }
}

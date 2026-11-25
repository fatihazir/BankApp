using Application.Features.Auth.Commands.Login;
using Application.Features.AuditLogs.Queries.GetList;
using Application.Features.IndividualCustomers.Commands.Delete;
using Application.Features.LoanApplications.Commands.Approve;
using Application.Features.LoanApplications.Commands.Create;
using Application.Features.LoanApplications.Commands.Reject;
using Application.Features.LoanApplications.Commands.StartReview;
using Application.Features.Users.Commands.Ban;
using Application.Features.Users.Commands.CreateStaff;
using Application.Features.Users.Commands.Unban;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Persistence.EntityConfigurations;
using Persistence.IntegrationTests.Infrastructure;

namespace Persistence.IntegrationTests.Security;

/// <summary>Who did what, to what and from where: written by the pipeline, read by staff.</summary>
[Collection(DatabaseCollection.Name)]
public class AuditTrailTests(SqlServerFixture database) : IntegrationTestBase(database)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<List<AuditLog>> AllEntries() => UsingDbContext(db => db.AuditLogs.AsNoTracking().OrderBy(l => l.CreateDate).ToListAsync(Ct));

    private async Task<(Guid CustomerId, Guid ApplicationId)> CustomerWithApplicationAsync()
    {
        Guid customerId = (await Send(TestData.IndividualCreate())).Id;
        Guid applicationId = (await Send(new CreateLoanApplicationCommand
        {
            CustomerId = customerId, LoanSubTypeId = LoanSeedData.PersonalLoanId, RequestedAmount = 50_000, TermInMonths = 12
        })).Id;
        return (customerId, applicationId);
    }

    [Fact]
    public async Task TheWholeDecisionFlow_LeavesWhoDidWhat()
    {
        var (customerId, applicationId) = await CustomerWithApplicationAsync();
        Clock.Advance(TimeSpan.FromMinutes(1));
        await Send(new StartReviewLoanApplicationCommand { Id = applicationId });
        Clock.Advance(TimeSpan.FromMinutes(1));
        await Send(new ApproveLoanApplicationCommand { Id = applicationId, ApprovedAmount = 40_000, Reason = "Good profile" });

        var entries = await AllEntries();

        entries.Select(e => e.Action).ShouldBe(
            ["Customer.Individual.Create", "LoanApplication.Create", "LoanApplication.StartReview", "LoanApplication.Approve"]);
        entries[0].TargetId.ShouldBe(customerId);
        var approve = entries[3];
        (approve.TargetType, approve.TargetId, approve.Detail).ShouldBe(("LoanApplication", applicationId, "Approved amount 40000. Good profile"));
        (approve.ActorUserId, approve.ActorEmail, approve.ActorRole, approve.IpAddress, approve.Succeeded)
            .ShouldBe((CurrentUser.Id, CurrentUser.Email, "BankStaff", "203.0.113.7", true));
        approve.CreateDate.ShouldBe(Clock.Now.UtcDateTime); // the moment comes from the clock
    }

    [Fact]
    public async Task BansUnbansDeletesAndNewStaff_AreRecordedWithTheReason()
    {
        Guid customerId = (await Send(TestData.IndividualCreate())).Id;
        await Send(new BanUserCommand { Id = customerId, Reason = "Suspicious transfers" });
        await Send(new UnbanUserCommand { Id = customerId });
        Guid staffId = (await Send(new CreateStaffUserCommand { Email = "new.staff@bank.test", Password = TestData.Password })).UserId;
        await Send(new DeleteIndividualCustomerCommand { Id = customerId });

        var entries = (await AllEntries()).Where(e => e.Action != "Customer.Individual.Create").ToList();

        entries.Select(e => (e.Action, e.TargetId)).ShouldBe(
            [("User.Ban", customerId), ("User.Unban", customerId), ("User.CreateStaff", staffId), ("Customer.Individual.Delete", customerId)]);
        entries[0].Detail.ShouldBe("Suspicious transfers");
    }

    [Fact]
    public async Task NoSecretEverReachesTheTrail()
    {
        await Send(TestData.IndividualCreate());
        await Send(new CreateStaffUserCommand { Email = "new.staff@bank.test", Password = TestData.Password });

        var text = string.Join("|", (await AllEntries()).Select(e =>
            $"{e.ActorEmail}|{e.Action}|{e.TargetType}|{e.Detail}|{e.IpAddress}|{e.TraceId}"));

        text.ShouldNotContain(TestData.Password);
        text.ShouldNotContain("PBKDF2");
        text.ShouldNotContain(TestData.NationalId(1)); // personal data stays out of the trail as well
    }

    [Fact]
    public async Task ARequestThatFails_LeavesNothing()
    {
        await Send(TestData.IndividualCreate());
        int before = (await AllEntries()).Count;

        await Should.ThrowAsync<BusinessException>(() => Send(TestData.IndividualCreate())); // same national ID
        await Should.ThrowAsync<NotFoundException>(() => Send(new StartReviewLoanApplicationCommand { Id = Guid.NewGuid() }));
        await Should.ThrowAsync<ValidationException>(() => Send(new RejectLoanApplicationCommand { Id = Guid.NewGuid() }));

        (await AllEntries()).Count.ShouldBe(before);
    }

    [Fact]
    public async Task TheTrail_OutlivesTheCustomer()
    {
        Guid customerId = (await Send(TestData.IndividualCreate())).Id;
        await Send(new DeleteIndividualCustomerCommand { Id = customerId });

        (await AllEntries()).Count(e => e.TargetId == customerId).ShouldBe(2); // create and delete are both still there
    }

    // ---- logins ----

    [Fact]
    public async Task LoginsRefusalsAndALockout_AreRecorded()
    {
        Guid customerId = (await Send(TestData.IndividualCreate())).Id;
        CurrentUser.AsAnonymous();
        var wrong = new LoginCommand { Email = TestData.Email(1), Password = "Wrong-Password-1" };

        for (int i = 0; i < 5; i++)
            await Should.ThrowAsync<AuthenticationException>(() => Send(wrong));
        Clock.Advance(TimeSpan.FromMinutes(16)); // the lockout is over
        await Send(new LoginCommand { Email = TestData.Email(1), Password = TestData.Password });

        var logins = (await AllEntries()).Where(e => e.Action.StartsWith("Auth.")).ToList();
        logins.Select(e => e.Action).ShouldBe(
            ["Auth.Login.Failed", "Auth.Login.Failed", "Auth.Login.Failed", "Auth.Login.Failed", "Auth.Login.Failed", "Auth.Login.LockedOut", "Auth.Login.Succeeded"]);
        logins.ShouldAllBe(e => e.ActorUserId == customerId);
        logins.Count(e => !e.Succeeded).ShouldBe(6);
        logins.Last().Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task AnUnknownEmail_LeavesNoTrace()
    {
        CurrentUser.AsAnonymous();

        await Should.ThrowAsync<AuthenticationException>(() => Send(new LoginCommand { Email = "nobody@bank.test", Password = "Wrong-Password-1" }));

        (await AllEntries()).ShouldBeEmpty();
    }

    // ---- reading ----

    private async Task SeedAsync()
    {
        var (customerId, applicationId) = await CustomerWithApplicationAsync();
        Clock.Advance(TimeSpan.FromHours(1));
        await Send(new StartReviewLoanApplicationCommand { Id = applicationId });
        Clock.Advance(TimeSpan.FromHours(1));
        await Send(new BanUserCommand { Id = customerId, Reason = "x" });
    }

    [Fact]
    public async Task TheList_IsNewestFirst_AndPaged()
    {
        await SeedAsync();

        var first = await Send(new GetListAuditLogQuery { Index = 0, Size = 2 });
        var second = await Send(new GetListAuditLogQuery { Index = 1, Size = 2 });

        first.Count.ShouldBe(4);
        first.Items.Select(i => i.Action).ShouldBe(["User.Ban", "LoanApplication.StartReview"]);
        second.Items.Select(i => i.Action).ShouldBe(["LoanApplication.Create", "Customer.Individual.Create"]);
        (first.HasNext, second.HasNext).ShouldBe((true, false));
    }

    [Fact]
    public async Task TheFilters_NarrowTheList()
    {
        await SeedAsync();
        var all = await AllEntries();
        Guid customerId = all.First(e => e.Action == "Customer.Individual.Create").TargetId!.Value;

        (await Send(new GetListAuditLogQuery { Action = "LoanApplication" })).Count.ShouldBe(2);
        (await Send(new GetListAuditLogQuery { Action = "User." })).Count.ShouldBe(1);
        (await Send(new GetListAuditLogQuery { TargetId = customerId })).Count.ShouldBe(2); // created and banned
        (await Send(new GetListAuditLogQuery { ActorUserId = CurrentUser.Id })).Count.ShouldBe(4);
        (await Send(new GetListAuditLogQuery { ActorUserId = Guid.NewGuid() })).Count.ShouldBe(0);
        (await Send(new GetListAuditLogQuery { Succeeded = false })).Count.ShouldBe(0);

        DateTime start = all[0].CreateDate;
        (await Send(new GetListAuditLogQuery { From = start.AddMinutes(90) })).Count.ShouldBe(1);
        (await Send(new GetListAuditLogQuery { To = start.AddMinutes(90) })).Count.ShouldBe(3);
        (await Send(new GetListAuditLogQuery { From = start.AddMinutes(30), To = start.AddMinutes(90) })).Count.ShouldBe(1);
    }

    [Fact]
    public async Task OnlyStaffCanRead()
    {
        await SeedAsync();
        CurrentUser.AsCustomer(Guid.NewGuid());
        await Should.ThrowAsync<AuthorizationException>(() => Send(new GetListAuditLogQuery()));

        CurrentUser.AsAnonymous();
        await Should.ThrowAsync<AuthenticationException>(() => Send(new GetListAuditLogQuery()));
    }
}

using Application.Features.Auth.Commands.Login;
using Application.Features.Auth.Queries.Me;
using Application.Features.CorporateCustomers.Commands.Create;
using Application.Features.CorporateCustomers.Queries.GetById;
using Application.Features.CorporateCustomers.Queries.GetList;
using Application.Features.IndividualCustomers.Commands.Create;
using Application.Features.IndividualCustomers.Commands.Delete;
using Application.Features.IndividualCustomers.Commands.Update;
using Application.Features.IndividualCustomers.Queries.GetById;
using Application.Features.IndividualCustomers.Queries.GetList;
using Application.Features.LoanApplications.Commands.Approve;
using Application.Features.LoanApplications.Commands.Cancel;
using Application.Features.LoanApplications.Commands.Create;
using Application.Features.LoanApplications.Commands.Reject;
using Application.Features.LoanApplications.Commands.StartReview;
using Application.Features.LoanApplications.Queries.Calculate;
using Application.Features.LoanApplications.Queries.GetById;
using Application.Features.LoanApplications.Queries.GetList;
using Application.Features.LoanSubTypes.Commands.Create;
using Application.Features.LoanTypes.Commands.Create;
using Application.Features.LoanTypes.Queries.GetList;
using Application.Features.Users.Commands.CreateStaff;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MediatR;
using Persistence.EntityConfigurations;
using Persistence.IntegrationTests.Infrastructure;

namespace Persistence.IntegrationTests.Security;

/// <summary>The roles and ownership rules, tried against a real database.</summary>
[Collection(DatabaseCollection.Name)]
public class AuthorizationFlowTests(SqlServerFixture database) : IntegrationTestBase(database)
{
    private const string Password = "Correct-Horse-9";

    private sealed record Setup(Guid FatihId, Guid SevvalId, Guid FatihApplicationId, Guid SevvalApplicationId);

    /// <summary>Two individual customers, each with one loan application. Created as staff.</summary>
    private async Task<Setup> TwoCustomersWithApplicationsAsync()
    {
        CurrentUser.AsStaff();
        Guid fatih = (await Send(TestData.IndividualCreate(1))).Id;
        Guid sevval = (await Send(TestData.IndividualCreate(2))).Id;
        Guid fatihApp = (await Send(Apply(fatih))).Id;
        Guid sevvalApp = (await Send(Apply(sevval))).Id;
        return new Setup(fatih, sevval, fatihApp, sevvalApp);
    }

    private static CreateLoanApplicationCommand Apply(Guid customerId) => new()
    {
        CustomerId = customerId, LoanSubTypeId = LoanSeedData.PersonalLoanId, RequestedAmount = 100_000, TermInMonths = 24
    };

    // ---- nobody logged in ----

    [Fact]
    public async Task Anonymous_CannotRunAnySecuredRequest()
    {
        CurrentUser.AsAnonymous();

        await Should.ThrowAsync<AuthenticationException>(() => Send(new GetListIndividualCustomerQuery()));
        await Should.ThrowAsync<AuthenticationException>(() => Send(new GetListLoanTypeQuery()));
        await Should.ThrowAsync<AuthenticationException>(() => Send(new CalculateLoanQuery { LoanSubTypeId = LoanSeedData.PersonalLoanId, Amount = 10_000, TermInMonths = 12 }));
        await Should.ThrowAsync<AuthenticationException>(() => Send(new MeQuery()));
        await Should.ThrowAsync<AuthenticationException>(() => Send(TestData.IndividualCreate()));
    }

    [Fact]
    public async Task Anonymous_IsRefusedBeforeValidation_SoNothingAboutTheRulesLeaks()
    {
        CurrentUser.AsAnonymous();

        // An empty body would fail validation, but an anonymous caller must get 401 first.
        await Should.ThrowAsync<AuthenticationException>(() => Send(new CreateIndividualCustomerCommand()));
    }

    // ---- staff-only operations ----

    [Fact]
    public async Task Customer_CannotRunStaffOnlyRequests()
    {
        var setup = await TwoCustomersWithApplicationsAsync();
        CurrentUser.AsCustomer(setup.FatihId);

        await Should.ThrowAsync<AuthorizationException>(() => Send(TestData.IndividualCreate(9)));
        await Should.ThrowAsync<AuthorizationException>(() => Send(TestData.CorporateCreate(9)));
        await Should.ThrowAsync<AuthorizationException>(() => Send(new DeleteIndividualCustomerCommand { Id = setup.FatihId }));
        await Should.ThrowAsync<AuthorizationException>(() => Send(new GetListIndividualCustomerQuery()));
        await Should.ThrowAsync<AuthorizationException>(() => Send(new GetListCorporateCustomerQuery()));
        await Should.ThrowAsync<AuthorizationException>(() => Send(new StartReviewLoanApplicationCommand { Id = setup.FatihApplicationId }));
        await Should.ThrowAsync<AuthorizationException>(() => Send(new ApproveLoanApplicationCommand { Id = setup.FatihApplicationId, ApprovedAmount = 1 }));
        await Should.ThrowAsync<AuthorizationException>(() => Send(new RejectLoanApplicationCommand { Id = setup.FatihApplicationId, Reason = "x" }));
        await Should.ThrowAsync<AuthorizationException>(() => Send(new CreateLoanTypeCommand { Name = "X", Segment = CustomerSegment.Individual }));
        await Should.ThrowAsync<AuthorizationException>(() => Send(new CreateLoanSubTypeCommand { LoanTypeId = LoanSeedData.IndividualLoanTypeId, Name = "X" }));
        await Should.ThrowAsync<AuthorizationException>(() => Send(new CreateStaffUserCommand { Email = "a@b.com", Password = Password }));
    }

    [Fact]
    public async Task CustomerCannotApproveTheirOwnApplication_TheStatusStaysSubmitted()
    {
        var setup = await TwoCustomersWithApplicationsAsync();
        CurrentUser.AsCustomer(setup.FatihId);

        await Should.ThrowAsync<AuthorizationException>(() => Send(new ApproveLoanApplicationCommand { Id = setup.FatihApplicationId, ApprovedAmount = 100_000 }));

        (await Send(new GetByIdLoanApplicationQuery { Id = setup.FatihApplicationId })).Status.ShouldBe(LoanApplicationStatus.Submitted);
    }

    // ---- a customer's own data ----

    [Fact]
    public async Task Customer_CanReadAndUpdateTheirOwnRecord()
    {
        var setup = await TwoCustomersWithApplicationsAsync();
        CurrentUser.AsCustomer(setup.FatihId);

        (await Send(new GetByIdIndividualCustomerQuery { Id = setup.FatihId })).FirstName.ShouldBe("Ali");

        var updated = await Send(new UpdateIndividualCustomerCommand
        {
            Id = setup.FatihId, PhoneNumber = "5551112233", Address = "New street", City = "Ankara", Country = "Turkey",
            IsActive = true, FirstName = "Ali", LastName = "Veli", NationalId = TestData.NationalId(1), DateOfBirth = new DateOnly(1990, 1, 1),
            EmploymentStatus = EmploymentStatus.Employed
        });
        updated.Id.ShouldBe(setup.FatihId);
    }

    [Fact]
    public async Task Customer_CannotReadOrUpdateSomebodyElsesRecord_ItLooksLikeItDoesNotExist()
    {
        var setup = await TwoCustomersWithApplicationsAsync();
        CurrentUser.AsCustomer(setup.FatihId);

        await Should.ThrowAsync<NotFoundException>(() => Send(new GetByIdIndividualCustomerQuery { Id = setup.SevvalId }));
        await Should.ThrowAsync<NotFoundException>(() => Send(new UpdateIndividualCustomerCommand
        {
            Id = setup.SevvalId, PhoneNumber = "1", Address = "a", City = "a", Country = "a", FirstName = "Hacked",
            LastName = "x", NationalId = TestData.NationalId(2), DateOfBirth = new DateOnly(1990, 1, 1), EmploymentStatus = EmploymentStatus.Employed
        }));

        // The record of the other customer is untouched, and an id that does not exist gives the same answer.
        CurrentUser.AsStaff();
        (await Send(new GetByIdIndividualCustomerQuery { Id = setup.SevvalId })).FirstName.ShouldBe("Ali");
        CurrentUser.AsCustomer(setup.FatihId);
        await Should.ThrowAsync<NotFoundException>(() => Send(new GetByIdIndividualCustomerQuery { Id = Guid.NewGuid() }));
    }

    [Fact]
    public async Task IndividualCustomer_CannotReadACorporateRecord_AndTheOtherWayAround()
    {
        CurrentUser.AsStaff();
        Guid person = (await Send(TestData.IndividualCreate())).Id;
        Guid company = (await Send(TestData.CorporateCreate())).Id;

        CurrentUser.AsCustomer(person);
        await Should.ThrowAsync<NotFoundException>(() => Send(new GetByIdCorporateCustomerQuery { Id = company }));

        CurrentUser.AsCustomer(company);
        await Should.ThrowAsync<NotFoundException>(() => Send(new GetByIdIndividualCustomerQuery { Id = person }));
        (await Send(new GetByIdCorporateCustomerQuery { Id = company })).CompanyName.ShouldBe("Acme");
    }

    // ---- loan applications ----

    [Fact]
    public async Task Customer_CanApplyForThemselves_ButNotForSomebodyElse()
    {
        CurrentUser.AsStaff();
        Guid fatih = (await Send(TestData.IndividualCreate(1))).Id;
        Guid sevval = (await Send(TestData.IndividualCreate(2))).Id;

        CurrentUser.AsCustomer(fatih);
        (await Send(Apply(fatih))).CustomerId.ShouldBe(fatih);
        await Should.ThrowAsync<AuthorizationException>(() => Send(Apply(sevval)));

        CurrentUser.AsStaff();
        (await Send(new GetListLoanApplicationQuery { CustomerId = sevval })).Count.ShouldBe(0);
    }

    [Fact]
    public async Task Staff_CanApplyOnBehalfOfAnyCustomer()
    {
        CurrentUser.AsStaff();
        Guid fatih = (await Send(TestData.IndividualCreate())).Id;

        (await Send(Apply(fatih))).CustomerId.ShouldBe(fatih);
    }

    [Fact]
    public async Task Customer_SeesOnlyTheirOwnApplications_InTheList()
    {
        var setup = await TwoCustomersWithApplicationsAsync();
        CurrentUser.AsCustomer(setup.FatihId);

        var list = await Send(new GetListLoanApplicationQuery { Size = 50 });

        list.Count.ShouldBe(1);
        list.Items.Single().Id.ShouldBe(setup.FatihApplicationId);
    }

    [Fact]
    public async Task Customer_CannotWidenTheList_ByAskingForAnotherCustomer()
    {
        var setup = await TwoCustomersWithApplicationsAsync();
        CurrentUser.AsCustomer(setup.FatihId);

        await Should.ThrowAsync<AuthorizationException>(() => Send(new GetListLoanApplicationQuery { CustomerId = setup.SevvalId }));
        (await Send(new GetListLoanApplicationQuery { CustomerId = setup.FatihId })).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Staff_SeesEveryApplication()
    {
        await TwoCustomersWithApplicationsAsync();
        CurrentUser.AsStaff();

        (await Send(new GetListLoanApplicationQuery { Size = 50 })).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Customer_CanReadTheirApplication_ButNotSomebodyElses()
    {
        var setup = await TwoCustomersWithApplicationsAsync();
        CurrentUser.AsCustomer(setup.FatihId);

        (await Send(new GetByIdLoanApplicationQuery { Id = setup.FatihApplicationId })).CustomerId.ShouldBe(setup.FatihId);
        await Should.ThrowAsync<NotFoundException>(() => Send(new GetByIdLoanApplicationQuery { Id = setup.SevvalApplicationId }));
    }

    [Fact]
    public async Task Customer_CanCancelTheirApplication_ButNotSomebodyElses()
    {
        var setup = await TwoCustomersWithApplicationsAsync();
        CurrentUser.AsCustomer(setup.FatihId);

        await Should.ThrowAsync<NotFoundException>(() => Send(new CancelLoanApplicationCommand { Id = setup.SevvalApplicationId }));
        (await Send(new CancelLoanApplicationCommand { Id = setup.FatihApplicationId })).Status.ShouldBe(LoanApplicationStatus.Cancelled);

        CurrentUser.AsStaff();
        (await Send(new GetByIdLoanApplicationQuery { Id = setup.SevvalApplicationId })).Status.ShouldBe(LoanApplicationStatus.Submitted);
    }

    [Fact]
    public async Task Staff_CanRunTheWholeDecisionFlow_OnAnyCustomersApplication()
    {
        var setup = await TwoCustomersWithApplicationsAsync();
        CurrentUser.AsStaff();

        await Send(new StartReviewLoanApplicationCommand { Id = setup.SevvalApplicationId });
        (await Send(new ApproveLoanApplicationCommand { Id = setup.SevvalApplicationId, ApprovedAmount = 90_000 })).Status.ShouldBe(LoanApplicationStatus.Approved);

        CurrentUser.AsCustomer(setup.SevvalId);
        (await Send(new GetByIdLoanApplicationQuery { Id = setup.SevvalApplicationId })).ApprovedAmount.ShouldBe(90_000);
    }

    [Fact]
    public async Task EveryLoggedInUser_CanBrowseLoanTypesAndCalculate()
    {
        var setup = await TwoCustomersWithApplicationsAsync();
        CurrentUser.AsCustomer(setup.FatihId);

        (await Send(new GetListLoanTypeQuery())).Count.ShouldBe(2);
        (await Send(new CalculateLoanQuery { LoanSubTypeId = LoanSeedData.PersonalLoanId, Amount = 10_000, TermInMonths = 12 })).Schedule.Count.ShouldBe(12);
    }

    // ---- accounts: a customer is a user, so it can log in right after it was created ----

    private async Task<Guid> NewCustomerAsync(int seed = 1)
    {
        CurrentUser.AsStaff();
        Guid id = (await Send(TestData.IndividualCreate(seed))).Id;
        CurrentUser.AsAnonymous();
        return id;
    }

    private static LoginCommand LoginAs(int seed = 1, string? password = null) =>
        new() { Email = TestData.Email(seed), Password = password ?? TestData.Password };

    [Fact]
    public async Task ANewCustomer_CanLoginAtOnce_AsThemselvesOnly()
    {
        Guid customerId = await NewCustomerAsync();

        var login = await Send(new LoginCommand { Email = " CUSTOMER1@Bank.test ", Password = TestData.Password });

        login.Role.ShouldBe(UserRole.Customer);
        login.CustomerId.ShouldBe(customerId);
        login.UserId.ShouldBe(customerId); // the customer is the user
        login.AccessToken.Split('.').Length.ShouldBe(3);
        login.RefreshToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task TheEmailOfANewCustomer_IsStoredNormalized()
    {
        CurrentUser.AsStaff();
        var command = TestData.IndividualCreate();
        command.Email = "  Mixed.Case@Bank.TEST ";
        await Send(command);

        var stored = await UsingDbContext(db => Task.FromResult(db.ApplicationUsers.Single()));

        stored.Email.ShouldBe("mixed.case@bank.test");
    }

    [Fact]
    public async Task ANewCustomer_WithAWrongPassword_CannotLogin()
    {
        await NewCustomerAsync();

        await Should.ThrowAsync<AuthenticationException>(() => Send(LoginAs(password: "Wrong-Password-1")));
    }

    [Fact]
    public async Task TheEmailIsTheLoginNameOfEveryUser_SoACustomerCannotTakeAStaffEmail_AndTheOtherWayAround()
    {
        CurrentUser.AsStaff();
        await Send(new CreateStaffUserCommand { Email = TestData.Email(1), Password = TestData.Password });

        await Should.ThrowAsync<BusinessException>(() => Send(TestData.IndividualCreate(1)));

        await Send(TestData.IndividualCreate(2));
        await Should.ThrowAsync<BusinessException>(() => Send(new CreateStaffUserCommand { Email = TestData.Email(2), Password = TestData.Password }));
    }

    [Fact]
    public async Task TwoCustomers_CannotShareAnEmail_AndNothingIsSavedForTheSecond()
    {
        CurrentUser.AsStaff();
        await Send(TestData.IndividualCreate(1));
        var second = TestData.IndividualCreate(2);
        second.Email = TestData.Email(1);

        await Should.ThrowAsync<BusinessException>(() => Send(second));

        (await UsingDbContext(db => db.IndividualCustomers.CountAsync(TestContext.Current.CancellationToken))).ShouldBe(1);
    }

    [Fact]
    public async Task ACustomerWithAWeakPassword_IsRefused_AndNothingIsSaved()
    {
        CurrentUser.AsStaff();
        var command = TestData.IndividualCreate();
        command.Password = "weak";

        await Should.ThrowAsync<ValidationException>(() => Send(command));

        (await UsingDbContext(db => db.Users.CountAsync(TestContext.Current.CancellationToken))).ShouldBe(0);
    }

    [Fact]
    public async Task WrongPassword_FiveTimes_LocksTheAccount_UntilItExpires()
    {
        await NewCustomerAsync();

        for (int i = 0; i < 5; i++)
            await Should.ThrowAsync<AuthenticationException>(() => Send(LoginAs(password: "Wrong-Password-1")));

        var locked = await Should.ThrowAsync<AuthenticationException>(() => Send(LoginAs()));
        locked.Message.ShouldContain("locked");

        await UsingDbContext(async db =>
        {
            db.ApplicationUsers.Single().LockoutEnd = DateTime.UtcNow.AddMinutes(-1); // the lockout period passes
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        (await Send(LoginAs())).AccessToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_Failures_AndSuccesses_AreStoredOnTheUser()
    {
        await NewCustomerAsync();

        await Should.ThrowAsync<AuthenticationException>(() => Send(LoginAs(password: "Wrong-Password-1")));
        var afterFailure = await UsingDbContext(db => Task.FromResult(db.ApplicationUsers.Single()));
        (afterFailure.FailedLoginCount, afterFailure.LastLoginDate).ShouldBe((1, null));

        await Send(LoginAs());
        var afterSuccess = await UsingDbContext(db => Task.FromResult(db.ApplicationUsers.Single()));
        (afterSuccess.FailedLoginCount, afterSuccess.LastLoginDate is not null).ShouldBe((0, true));
    }

    [Fact]
    public async Task ThePassword_IsStoredHashed_NeverInPlainText()
    {
        await NewCustomerAsync();

        var user = await UsingDbContext(db => Task.FromResult(db.ApplicationUsers.Single()));

        user.PasswordHash.ShouldNotContain(TestData.Password);
        user.PasswordHash.ShouldStartWith("PBKDF2-SHA256$");
    }

    [Fact]
    public async Task Staff_CanCreateStaff_WhoCanLoginAndHaveNoCustomer()
    {
        CurrentUser.AsStaff();
        await Send(new CreateStaffUserCommand { Email = "new.staff@bank.test", Password = TestData.Password });
        CurrentUser.AsAnonymous();

        var login = await Send(new LoginCommand { Email = "new.staff@bank.test", Password = TestData.Password });

        login.Role.ShouldBe(UserRole.BankStaff);
        login.CustomerId.ShouldBeNull();
    }

    [Fact]
    public async Task Me_ReturnsTheCustomerOfTheToken()
    {
        Guid customerId = await NewCustomerAsync();
        CurrentUser.AsCustomer(customerId);

        var me = await Send(new MeQuery());

        (me.CustomerId, me.CustomerName, me.UserId).ShouldBe((customerId, "Ali Veli", customerId));
        me.Roles.ShouldBe([Application.Services.Security.BankRoles.Customer]);
        me.EmailVerified.ShouldBeFalse();
    }
}

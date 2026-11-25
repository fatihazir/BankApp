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
    public async Task Anonymous_IsRefusedBeforeValidation_SoNothingAboutTheRulesLeaks()
    {
        CurrentUser.AsAnonymous();

        // An empty body would fail validation, but an anonymous caller must get 401 first.
        await Should.ThrowAsync<AuthenticationException>(() => Send(new CreateIndividualCustomerCommand()));
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
}

using System.Linq.Expressions;
using Application.Features.Auth.Rules;
using Application.Features.CorporateCustomers.Commands.Create;
using Application.Features.CorporateCustomers.Commands.Delete;
using Application.Features.CorporateCustomers.Queries.GetById;
using Application.Features.CorporateCustomers.Rules;
using Application.Services.Repositories;
using Application.Services.Security;
using Application.UnitTests.Support;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using BankingSystemCore.Mailing;
using BankingSystemCore.Repositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Application.UnitTests.Features.CorporateCustomers;

public class CorporateCustomerHandlerTests
{
    private readonly AuthFixture _f = new();
    private readonly ICorporateCustomerRepository _repository = Substitute.For<ICorporateCustomerRepository>();
    private readonly CorporateCustomerBusinessRules _rules;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public CorporateCustomerHandlerTests()
    {
        _rules = new CorporateCustomerBusinessRules(_repository, TimeProvider.System);

        // By default a customer has no live sessions.
        _f.Sessions.GetListAsync(
                Arg.Any<Expression<Func<UserSession, bool>>?>(),
                Arg.Any<Func<IQueryable<UserSession>, IOrderedQueryable<UserSession>>?>(),
                Arg.Any<Func<IQueryable<UserSession>, IIncludableQueryable<UserSession, object?>>?>(),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new Paginate<UserSession>());
    }

    private CreateCorporateCustomerCommandHandler CreateHandler() => new(
        _repository, _rules, new AuthBusinessRules(_f.Users), _f.Hasher, _f.Verification,
        NullLogger<CreateCorporateCustomerCommandHandler>.Instance);

    private static CreateCorporateCustomerCommand ValidCommand() => new()
    {
        Email = "  New.Customer@Bank.test ",
        Password = AuthFixture.Password,
        CompanyName = "Acme",
        TaxNumber = "1234567890",
        FoundationDate = new DateOnly(2010, 5, 1)
    };

    // ---- creating a customer creates the whole user ----

    [Fact]
    public async Task Create_SavesOneCustomer_WithEveryLevelOfTheUserFilledIn()
    {
        CorporateCustomer? saved = null;
        await _repository.AddAsync(Arg.Do<CorporateCustomer>(c => saved = c), Arg.Any<CancellationToken>());

        var response = await CreateHandler().Handle(ValidCommand(), Ct);

        // One entity, one save: the database writes the rows of Users, ApplicationUsers, Customers and the type table.
        await _repository.Received(1).AddAsync(Arg.Any<CorporateCustomer>(), Arg.Any<CancellationToken>());
        saved.ShouldNotBeNull();
        saved.Email.ShouldBe("new.customer@bank.test");                              // Users: login name, trimmed and lower-cased
        _f.Hasher.Verify(AuthFixture.Password, saved.PasswordHash).ShouldBeTrue();  // Users: password hash
        saved.PasswordHash.ShouldNotBe(AuthFixture.Password);
        saved.IsActive.ShouldBeTrue();
        saved.EmailVerified.ShouldBeFalse();
        saved.Role.ShouldBe(UserRole.Customer);                                      // ApplicationUsers: always Customer
        saved.CustomerNumber.ShouldStartWith("COR-");                           // Customers
        saved.CompanyName.ShouldBe("Acme");                                      // type table
        response.CustomerNumber.ShouldBe(saved.CustomerNumber);
    }

    [Fact]
    public async Task Create_TheCaller_CannotChooseTheRole()
    {
        typeof(CreateCorporateCustomerCommand).GetProperty("Role").ShouldBeNull();

        CorporateCustomer? saved = null;
        await _repository.AddAsync(Arg.Do<CorporateCustomer>(c => saved = c), Arg.Any<CancellationToken>());
        await CreateHandler().Handle(ValidCommand(), Ct);

        saved!.Role.ShouldBe(UserRole.Customer);
    }

    [Fact]
    public async Task Create_TheNewCustomer_GetsAVerificationEmail_WithATokenThatIsStoredOnlyAsAHash()
    {
        CorporateCustomer? saved = null;
        await _repository.AddAsync(Arg.Do<CorporateCustomer>(c => saved = c), Arg.Any<CancellationToken>());

        await CreateHandler().Handle(ValidCommand(), Ct);

        saved!.EmailVerificationTokenHash.ShouldNotBeNullOrEmpty();
        saved.EmailVerificationTokenExpiresAt.ShouldBe(_f.Utc.AddHours(24));
        await _f.Sender.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.To == "new.customer@bank.test" && m.Body.Contains("token=") && !m.Body.Contains(saved.EmailVerificationTokenHash!)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WhenTheMailServerFails_TheCustomerIsStillCreated()
    {
        _f.Sender.SendAsync(default!, Ct).ReturnsForAnyArgs(Task.FromException(new InvalidOperationException("SMTP is down")));

        var response = await CreateHandler().Handle(ValidCommand(), Ct);

        response.CustomerNumber.ShouldStartWith("COR-");
        await _repository.Received(1).AddAsync(Arg.Any<CorporateCustomer>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_TheResponse_NeverContainsThePasswordOrItsHash()
    {
        var response = await CreateHandler().Handle(ValidCommand(), Ct);

        var json = System.Text.Json.JsonSerializer.Serialize(response);
        json.ShouldNotContain(AuthFixture.Password);
        json.ShouldNotContain("PBKDF2");
        typeof(CreatedCorporateCustomerResponse).GetProperties().ShouldNotContain(p => p.Name.Contains("Password") || p.Name.Contains("Hash"));
    }

    [Fact]
    public async Task Create_WithAnEmailThatBelongsToAnotherUser_IsRefused_AndNothingIsSavedOrSent()
    {
        _f.Users.AnyAsync(Arg.Any<Expression<Func<ApplicationUser, bool>>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(true);

        await Should.ThrowAsync<BusinessException>(() => CreateHandler().Handle(ValidCommand(), Ct));

        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, Ct);
        await _f.Sender.DidNotReceiveWithAnyArgs().SendAsync(default!, Ct);
    }

    [Fact]
    public async Task Create_TheEmailCheck_IncludesDeletedUsers_BecauseTheDatabaseIndexDoes()
    {
        await CreateHandler().Handle(ValidCommand(), Ct);

        await _f.Users.Received(1).AnyAsync(Arg.Any<Expression<Func<ApplicationUser, bool>>?>(), true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_Throws_AndDoesNotSave_WhenTaxNumberAlreadyExists()
    {
        _repository.AnyAsync(Arg.Any<Expression<Func<CorporateCustomer, bool>>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(true);

        await Should.ThrowAsync<BusinessException>(() => CreateHandler().Handle(ValidCommand(), Ct));

        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, Ct);
    }

    [Fact]
    public async Task Create_Throws_WhenTheFoundationDateIsInTheFuture()
    {
        var command = ValidCommand();
        command.FoundationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

        await Should.ThrowAsync<BusinessException>(() => CreateHandler().Handle(command, Ct));
    }


    // ---- reading ----

    [Fact]
    public async Task GetById_Throws_WhenCustomerDoesNotExist()
    {
        var handler = new GetByIdCorporateCustomerQueryHandler(_repository, _rules, FakeCurrentUser.Staff().Policy());

        await Should.ThrowAsync<NotFoundException>(
            () => handler.Handle(new GetByIdCorporateCustomerQuery { Id = Guid.NewGuid() }, Ct));
    }

    // ---- deleting ----

    private DeleteCorporateCustomerCommandHandler DeleteHandler() => new(_repository, _rules, new UserSessionService(_f.Sessions, _f.Time));

    private void CustomerIs(CorporateCustomer? customer) =>
        _repository.GetAsync(
                Arg.Any<Expression<Func<CorporateCustomer, bool>>>(),
                Arg.Any<Func<IQueryable<CorporateCustomer>, IIncludableQueryable<CorporateCustomer, object?>>?>(),
                Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(customer);

    [Fact]
    public async Task Delete_DeactivatesTheUser_SoItCannotLoginAnymore()
    {
        var customer = new CorporateCustomer { Id = Guid.NewGuid(), IsActive = true };
        CustomerIs(customer);

        await DeleteHandler().Handle(new DeleteCorporateCustomerCommand { Id = customer.Id }, Ct);

        customer.IsActive.ShouldBeFalse();
        await _repository.Received(1).DeleteAsync(customer, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_EndsEverySessionOfTheCustomer_SoLiveTokensStopAtOnce()
    {
        var customer = new CorporateCustomer { Id = Guid.NewGuid() };
        CustomerIs(customer);
        var sessions = new List<UserSession> { new() { Id = Guid.NewGuid() }, new() { Id = Guid.NewGuid() } };
        _f.Sessions.GetListAsync(
                Arg.Any<Expression<Func<UserSession, bool>>?>(),
                Arg.Any<Func<IQueryable<UserSession>, IOrderedQueryable<UserSession>>?>(),
                Arg.Any<Func<IQueryable<UserSession>, IIncludableQueryable<UserSession, object?>>?>(),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new Paginate<UserSession> { Items = sessions });

        await DeleteHandler().Handle(new DeleteCorporateCustomerCommand { Id = customer.Id }, Ct);

        sessions.ShouldAllBe(s => s.RevokedDate == _f.Utc && s.RevokeReason == "Customer deleted");
    }

    [Fact]
    public async Task Delete_AnUnknownCustomer_IsNotFound_AndNoSessionIsTouched()
    {
        CustomerIs(null);

        await Should.ThrowAsync<NotFoundException>(() => DeleteHandler().Handle(new DeleteCorporateCustomerCommand { Id = Guid.NewGuid() }, Ct));

        await _f.Sessions.DidNotReceiveWithAnyArgs().UpdateRangeAsync(default!, Ct);
    }
}

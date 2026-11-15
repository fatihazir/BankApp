using System.Linq.Expressions;
using Application.Features.Auth.Rules;
using Application.Features.IndividualCustomers.Commands.Create;
using Application.Features.IndividualCustomers.Commands.Delete;
using Application.Features.IndividualCustomers.Queries.GetById;
using Application.Features.IndividualCustomers.Rules;
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

namespace Application.UnitTests.Features.IndividualCustomers;

public class IndividualCustomerHandlerTests
{
    private readonly AuthFixture _f = new();
    private readonly IIndividualCustomerRepository _repository = Substitute.For<IIndividualCustomerRepository>();
    private readonly IndividualCustomerBusinessRules _rules;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public IndividualCustomerHandlerTests()
    {
        _rules = new IndividualCustomerBusinessRules(_repository);

        // By default a customer has no live sessions.
        _f.Sessions.GetListAsync(
                Arg.Any<Expression<Func<UserSession, bool>>?>(),
                Arg.Any<Func<IQueryable<UserSession>, IOrderedQueryable<UserSession>>?>(),
                Arg.Any<Func<IQueryable<UserSession>, IIncludableQueryable<UserSession, object?>>?>(),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new Paginate<UserSession>());
    }

    private CreateIndividualCustomerCommandHandler CreateHandler() => new(
        _repository, _rules, new AuthBusinessRules(_f.Users), _f.Hasher, _f.Verification,
        NullLogger<CreateIndividualCustomerCommandHandler>.Instance);

    private static CreateIndividualCustomerCommand ValidCommand() => new()
    {
        Email = "  New.Customer@Bank.test ",
        Password = AuthFixture.Password,
        FirstName = "Ali",
        LastName = "Veli",
        NationalId = "10000000146",
        DateOfBirth = new DateOnly(1990, 1, 1)
    };

    // ---- creating a customer creates the whole user ----

    [Fact]
    public async Task Create_SavesOneCustomer_WithEveryLevelOfTheUserFilledIn()
    {
        IndividualCustomer? saved = null;
        await _repository.AddAsync(Arg.Do<IndividualCustomer>(c => saved = c), Arg.Any<CancellationToken>());

        var response = await CreateHandler().Handle(ValidCommand(), Ct);

        // One entity, one save: the database writes the rows of Users, ApplicationUsers, Customers and the type table.
        await _repository.Received(1).AddAsync(Arg.Any<IndividualCustomer>(), Arg.Any<CancellationToken>());
        saved.ShouldNotBeNull();
        saved.Email.ShouldBe("new.customer@bank.test");                              // Users: login name, trimmed and lower-cased
        _f.Hasher.Verify(AuthFixture.Password, saved.PasswordHash).ShouldBeTrue();  // Users: password hash
        saved.PasswordHash.ShouldNotBe(AuthFixture.Password);
        saved.IsActive.ShouldBeTrue();
        saved.EmailVerified.ShouldBeFalse();
        saved.Role.ShouldBe(UserRole.Customer);                                      // ApplicationUsers: always Customer
        saved.CustomerNumber.ShouldStartWith("IND-");                           // Customers
        saved.FirstName.ShouldBe("Ali");                                      // type table
        response.CustomerNumber.ShouldBe(saved.CustomerNumber);
    }

    [Fact]
    public async Task Create_TheCaller_CannotChooseTheRole()
    {
        typeof(CreateIndividualCustomerCommand).GetProperty("Role").ShouldBeNull();

        IndividualCustomer? saved = null;
        await _repository.AddAsync(Arg.Do<IndividualCustomer>(c => saved = c), Arg.Any<CancellationToken>());
        await CreateHandler().Handle(ValidCommand(), Ct);

        saved!.Role.ShouldBe(UserRole.Customer);
    }

    [Fact]
    public async Task Create_TheNewCustomer_GetsAVerificationEmail_WithATokenThatIsStoredOnlyAsAHash()
    {
        IndividualCustomer? saved = null;
        await _repository.AddAsync(Arg.Do<IndividualCustomer>(c => saved = c), Arg.Any<CancellationToken>());

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

        response.CustomerNumber.ShouldStartWith("IND-");
        await _repository.Received(1).AddAsync(Arg.Any<IndividualCustomer>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_TheResponse_NeverContainsThePasswordOrItsHash()
    {
        var response = await CreateHandler().Handle(ValidCommand(), Ct);

        var json = System.Text.Json.JsonSerializer.Serialize(response);
        json.ShouldNotContain(AuthFixture.Password);
        json.ShouldNotContain("PBKDF2");
        typeof(CreatedIndividualCustomerResponse).GetProperties().ShouldNotContain(p => p.Name.Contains("Password") || p.Name.Contains("Hash"));
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
    public async Task Create_Throws_AndDoesNotSave_WhenNationalIdAlreadyExists()
    {
        _repository.AnyAsync(Arg.Any<Expression<Func<IndividualCustomer, bool>>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(true);

        await Should.ThrowAsync<BusinessException>(() => CreateHandler().Handle(ValidCommand(), Ct));

        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, Ct);
    }

    [Fact]
    public async Task Create_Throws_WhenNationalIdChecksumIsInvalid()
    {
        var command = ValidCommand();
        command.NationalId = "12345678901";

        await Should.ThrowAsync<BusinessException>(() => CreateHandler().Handle(command, Ct));
    }

    [Fact]
    public async Task Create_Throws_WhenTheCustomerIsUnderEighteen()
    {
        var command = ValidCommand();
        command.DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-10));

        await Should.ThrowAsync<BusinessException>(() => CreateHandler().Handle(command, Ct));
    }


    // ---- reading ----

    [Fact]
    public async Task GetById_Throws_WhenCustomerDoesNotExist()
    {
        var handler = new GetByIdIndividualCustomerQueryHandler(_repository, _rules, FakeCurrentUser.Staff().Policy());

        await Should.ThrowAsync<NotFoundException>(
            () => handler.Handle(new GetByIdIndividualCustomerQuery { Id = Guid.NewGuid() }, Ct));
    }

    // ---- deleting ----

    private DeleteIndividualCustomerCommandHandler DeleteHandler() => new(_repository, _rules, new UserSessionService(_f.Sessions, _f.Time));

    private void CustomerIs(IndividualCustomer? customer) =>
        _repository.GetAsync(
                Arg.Any<Expression<Func<IndividualCustomer, bool>>>(),
                Arg.Any<Func<IQueryable<IndividualCustomer>, IIncludableQueryable<IndividualCustomer, object?>>?>(),
                Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(customer);

    [Fact]
    public async Task Delete_DeactivatesTheUser_SoItCannotLoginAnymore()
    {
        var customer = new IndividualCustomer { Id = Guid.NewGuid(), IsActive = true };
        CustomerIs(customer);

        await DeleteHandler().Handle(new DeleteIndividualCustomerCommand { Id = customer.Id }, Ct);

        customer.IsActive.ShouldBeFalse();
        await _repository.Received(1).DeleteAsync(customer, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_EndsEverySessionOfTheCustomer_SoLiveTokensStopAtOnce()
    {
        var customer = new IndividualCustomer { Id = Guid.NewGuid() };
        CustomerIs(customer);
        var sessions = new List<UserSession> { new() { Id = Guid.NewGuid() }, new() { Id = Guid.NewGuid() } };
        _f.Sessions.GetListAsync(
                Arg.Any<Expression<Func<UserSession, bool>>?>(),
                Arg.Any<Func<IQueryable<UserSession>, IOrderedQueryable<UserSession>>?>(),
                Arg.Any<Func<IQueryable<UserSession>, IIncludableQueryable<UserSession, object?>>?>(),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new Paginate<UserSession> { Items = sessions });

        await DeleteHandler().Handle(new DeleteIndividualCustomerCommand { Id = customer.Id }, Ct);

        sessions.ShouldAllBe(s => s.RevokedDate == _f.Utc && s.RevokeReason == "Customer deleted");
    }

    [Fact]
    public async Task Delete_AnUnknownCustomer_IsNotFound_AndNoSessionIsTouched()
    {
        CustomerIs(null);

        await Should.ThrowAsync<NotFoundException>(() => DeleteHandler().Handle(new DeleteIndividualCustomerCommand { Id = Guid.NewGuid() }, Ct));

        await _f.Sessions.DidNotReceiveWithAnyArgs().UpdateRangeAsync(default!, Ct);
    }
}

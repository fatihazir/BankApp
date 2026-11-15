using System.Linq.Expressions;
using Application.Features.IndividualCustomers.Commands.Update;
using Application.Features.IndividualCustomers.Queries.GetById;
using Application.Features.IndividualCustomers.Rules;
using Application.Features.LoanApplications.Commands.Cancel;
using Application.Features.LoanApplications.Commands.Create;
using Application.Features.LoanApplications.Queries.GetById;
using Application.Features.LoanApplications.Queries.GetList;
using Application.Features.LoanApplications.Rules;
using Application.Services.Repositories;
using Application.UnitTests.Support;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using BankingSystemCore.Repositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore.Query;
using NSubstitute;

namespace Application.UnitTests.Security;

/// <summary>Customers reach only their own data, staff reach everything.</summary>
public class CustomerAccessTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---- the policy itself ----

    [Fact]
    public void Staff_CanAccessAnyCustomer()
    {
        var policy = FakeCurrentUser.Staff().Policy();

        policy.IsStaff.ShouldBeTrue();
        policy.CanAccess(Guid.NewGuid()).ShouldBeTrue();
        policy.OwnCustomerId.ShouldBeNull();
    }

    [Fact]
    public void Customer_CanAccessOnlyTheirOwnRecord()
    {
        Guid own = Guid.NewGuid();
        var policy = FakeCurrentUser.Customer(own).Policy();

        policy.IsStaff.ShouldBeFalse();
        policy.OwnCustomerId.ShouldBe(own);
        policy.CanAccess(own).ShouldBeTrue();
        policy.CanAccess(Guid.NewGuid()).ShouldBeFalse();
    }

    [Fact]
    public void CustomerWithoutAUserId_CanAccessNothing()
    {
        var policy = FakeCurrentUser.CustomerWithoutId().Policy();

        policy.CanAccess(Guid.NewGuid()).ShouldBeFalse();
        policy.CanAccess(Guid.Empty).ShouldBeFalse();
    }

    [Fact]
    public void Anonymous_CanAccessNothing() =>
        FakeCurrentUser.Anonymous().Policy().CanAccess(Guid.NewGuid()).ShouldBeFalse();

    [Fact]
    public void EnsureCanAccess_ThrowsAuthorizationException_ForSomebodyElsesCustomer()
    {
        var policy = FakeCurrentUser.Customer(Guid.NewGuid()).Policy();

        Should.Throw<AuthorizationException>(() => policy.EnsureCanAccess(Guid.NewGuid()));
    }

    [Fact]
    public void ACustomerIs_TheUser_SoTheCustomerIdIsTheUserId()
    {
        Guid userId = Guid.NewGuid();

        FakeCurrentUser.Customer(userId).Policy().OwnCustomerId.ShouldBe(userId);
    }

    [Fact]
    public void Staff_HaveNoCustomerId_EvenThoughTheyAreUsers() =>
        FakeCurrentUser.Staff().Policy().OwnCustomerId.ShouldBeNull();

    // ---- customer records ----

    private static (GetByIdIndividualCustomerQueryHandler Handler, IIndividualCustomerRepository Repository) CustomerQuery(FakeCurrentUser user)
    {
        var repository = Substitute.For<IIndividualCustomerRepository>();
        repository.GetAsync(
                Arg.Any<Expression<Func<IndividualCustomer, bool>>>(),
                Arg.Any<Func<IQueryable<IndividualCustomer>, IIncludableQueryable<IndividualCustomer, object?>>?>(),
                Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new IndividualCustomer { FirstName = "Fatih" });
        return (new GetByIdIndividualCustomerQueryHandler(repository, new IndividualCustomerBusinessRules(repository, TimeProvider.System), user.Policy()), repository);
    }

    [Fact]
    public async Task Customer_CanReadTheirOwnRecord()
    {
        Guid own = Guid.NewGuid();
        var (handler, _) = CustomerQuery(FakeCurrentUser.Customer(own));

        var result = await handler.Handle(new GetByIdIndividualCustomerQuery { Id = own }, Ct);

        result.FirstName.ShouldBe("Fatih");
    }

    [Fact]
    public async Task Customer_ReadingSomebodyElse_LooksLikeNotFound_AndNeverTouchesTheDatabase()
    {
        var (handler, repository) = CustomerQuery(FakeCurrentUser.Customer(Guid.NewGuid()));

        await Should.ThrowAsync<NotFoundException>(() => handler.Handle(new GetByIdIndividualCustomerQuery { Id = Guid.NewGuid() }, Ct));

        await repository.DidNotReceiveWithAnyArgs().GetAsync(default!, default, default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Staff_CanReadAnyCustomer()
    {
        var (handler, _) = CustomerQuery(FakeCurrentUser.Staff());

        (await handler.Handle(new GetByIdIndividualCustomerQuery { Id = Guid.NewGuid() }, Ct)).FirstName.ShouldBe("Fatih");
    }

    [Fact]
    public async Task Customer_CannotUpdateSomebodyElse()
    {
        var repository = Substitute.For<IIndividualCustomerRepository>();
        var handler = new UpdateIndividualCustomerCommandHandler(
            repository, new IndividualCustomerBusinessRules(repository, TimeProvider.System), FakeCurrentUser.Customer(Guid.NewGuid()).Policy());

        await Should.ThrowAsync<NotFoundException>(() => handler.Handle(new UpdateIndividualCustomerCommand { Id = Guid.NewGuid() }, Ct));

        await repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, Ct);
    }

    // ---- loan applications ----

    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
    private readonly ILoanSubTypeRepository _subTypes = Substitute.For<ILoanSubTypeRepository>();
    private readonly ILoanApplicationRepository _applications = Substitute.For<ILoanApplicationRepository>();

    private LoanApplicationBusinessRules Rules => new(_applications);

    private void ApplicationOf(Guid customerId) =>
        _applications.GetAsync(
                Arg.Any<Expression<Func<LoanApplication, bool>>>(),
                Arg.Any<Func<IQueryable<LoanApplication>, IIncludableQueryable<LoanApplication, object?>>?>(),
                Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new LoanApplication
            {
                CustomerId = customerId, Status = LoanApplicationStatus.Submitted, RequestedAmount = 1000, TermInMonths = 12,
                Customer = new IndividualCustomer(), LoanSubType = new LoanSubType { LoanType = new LoanType() }
            });

    [Fact]
    public async Task Customer_CannotApplyOnBehalfOfSomebodyElse_403()
    {
        var handler = new CreateLoanApplicationCommandHandler(
            _customers, _subTypes, _applications, Rules, FakeCurrentUser.Customer(Guid.NewGuid()).Policy(), TimeProvider.System);

        await Should.ThrowAsync<AuthorizationException>(() => handler.Handle(
            new CreateLoanApplicationCommand { CustomerId = Guid.NewGuid(), LoanSubTypeId = Guid.NewGuid(), RequestedAmount = 10, TermInMonths = 1 }, Ct));

        await _applications.DidNotReceiveWithAnyArgs().AddAsync(default!, Ct);
    }

    [Fact]
    public async Task Customer_ReadingSomebodyElsesApplication_LooksLikeNotFound()
    {
        ApplicationOf(Guid.NewGuid());
        var handler = new GetByIdLoanApplicationQueryHandler(_applications, Rules, FakeCurrentUser.Customer(Guid.NewGuid()).Policy());

        await Should.ThrowAsync<NotFoundException>(() => handler.Handle(new GetByIdLoanApplicationQuery { Id = Guid.NewGuid() }, Ct));
    }

    [Fact]
    public async Task Customer_CanReadTheirOwnApplication()
    {
        Guid own = Guid.NewGuid();
        ApplicationOf(own);
        var handler = new GetByIdLoanApplicationQueryHandler(_applications, Rules, FakeCurrentUser.Customer(own).Policy());

        (await handler.Handle(new GetByIdLoanApplicationQuery { Id = Guid.NewGuid() }, Ct)).CustomerId.ShouldBe(own);
    }

    [Fact]
    public async Task Customer_CannotCancelSomebodyElsesApplication_AndNothingIsSaved()
    {
        ApplicationOf(Guid.NewGuid());
        var handler = new CancelLoanApplicationCommandHandler(_applications, Rules, FakeCurrentUser.Customer(Guid.NewGuid()).Policy(), TimeProvider.System);

        await Should.ThrowAsync<NotFoundException>(() => handler.Handle(new CancelLoanApplicationCommand { Id = Guid.NewGuid() }, Ct));

        await _applications.DidNotReceiveWithAnyArgs().UpdateAsync(default!, Ct);
    }

    [Fact]
    public async Task Customer_CanCancelTheirOwnApplication()
    {
        Guid own = Guid.NewGuid();
        ApplicationOf(own);
        var handler = new CancelLoanApplicationCommandHandler(_applications, Rules, FakeCurrentUser.Customer(own).Policy(), TimeProvider.System);

        (await handler.Handle(new CancelLoanApplicationCommand { Id = Guid.NewGuid() }, Ct)).Status.ShouldBe(LoanApplicationStatus.Cancelled);
    }

    // ---- loan application list: the customer filter cannot be bypassed ----

    private Task<Paginate<Application.Features.LoanApplications.Queries.GetList.GetListLoanApplicationListItemDto>> List(
        FakeCurrentUser user, Guid? requestedCustomerId, out Func<Guid?> usedFilter)
    {
        Guid? used = Guid.Empty;
        _applications.GetListAsync(
                Arg.Do<Expression<Func<LoanApplication, bool>>?>(p =>
                {
                    // Probe the predicate to find out which customer it filters on.
                    var compiled = p!.Compile();
                    Guid? probe = null;
                    foreach (Guid candidate in _probe.Keys.ToArray())
                        if (compiled(new LoanApplication { CustomerId = candidate })) probe = candidate;
                    used = compiled(new LoanApplication { CustomerId = Guid.NewGuid() }) ? null : probe;
                }),
                Arg.Any<Func<IQueryable<LoanApplication>, IOrderedQueryable<LoanApplication>>?>(),
                Arg.Any<Func<IQueryable<LoanApplication>, IIncludableQueryable<LoanApplication, object?>>?>(),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new Paginate<LoanApplication>());
        usedFilter = () => used;
        var handler = new GetListLoanApplicationQueryHandler(_applications, user.Policy());
        return handler.Handle(new GetListLoanApplicationQuery { CustomerId = requestedCustomerId }, Ct);
    }

    private readonly Dictionary<Guid, bool> _probe = new();

    [Fact]
    public async Task Customer_ListIsAlwaysFilteredToTheirOwnApplications()
    {
        Guid own = Guid.NewGuid();
        _probe[own] = true;

        await List(FakeCurrentUser.Customer(own), requestedCustomerId: null, out var used);

        used().ShouldBe(own);
    }

    [Fact]
    public async Task Customer_AskingForTheirOwnId_IsFine()
    {
        Guid own = Guid.NewGuid();
        _probe[own] = true;

        await List(FakeCurrentUser.Customer(own), own, out var used);

        used().ShouldBe(own);
    }

    [Fact]
    public async Task Customer_AskingForSomebodyElsesList_IsForbidden()
    {
        await Should.ThrowAsync<AuthorizationException>(() => List(FakeCurrentUser.Customer(Guid.NewGuid()), Guid.NewGuid(), out _));
    }

    [Fact]
    public async Task CustomerWithoutAUserId_CannotListAnything()
    {
        await Should.ThrowAsync<AuthorizationException>(() => List(FakeCurrentUser.CustomerWithoutId(), null, out _));
    }

    [Fact]
    public async Task Staff_ListIsNotRestricted_AndCanFilterByAnyCustomer()
    {
        Guid someone = Guid.NewGuid();
        _probe[someone] = true;

        await List(FakeCurrentUser.Staff(), someone, out var filtered);
        filtered().ShouldBe(someone);

        await List(FakeCurrentUser.Staff(), null, out var unfiltered);
        unfiltered().ShouldBeNull();
    }
}

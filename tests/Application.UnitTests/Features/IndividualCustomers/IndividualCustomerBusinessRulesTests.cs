using System.Linq.Expressions;
using Application.Features.IndividualCustomers.Rules;
using Application.Services.Repositories;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Domain.Entities;
using Application.UnitTests.Support;
using NSubstitute;

namespace Application.UnitTests.Features.IndividualCustomers;

public class IndividualCustomerBusinessRulesTests
{
    private readonly IIndividualCustomerRepository _repository = Substitute.For<IIndividualCustomerRepository>();
    private readonly TestClock _clock = new(); // 2026-11-15
    private readonly IndividualCustomerBusinessRules _rules;

    public IndividualCustomerBusinessRulesTests()
    {
        _rules = new IndividualCustomerBusinessRules(_repository, _clock);
    }

    [Fact]
    public void CustomerShouldExist_Throws_WhenCustomerIsNull()
    {
        Should.Throw<NotFoundException>(() => _rules.CustomerShouldExist(null));
    }

    [Fact]
    public void CustomerShouldExist_DoesNotThrow_WhenCustomerExists()
    {
        Should.NotThrow(() => _rules.CustomerShouldExist(new IndividualCustomer()));
    }

    [Theory]
    [InlineData("10000000146")]
    [InlineData("12345678950")]
    [InlineData("23456789138")]
    public void NationalIdMustBeValid_Accepts_ValidIds(string nationalId)
    {
        Should.NotThrow(() => _rules.NationalIdMustBeValid(nationalId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567890")]       // too short
    [InlineData("123456789012")]     // too long
    [InlineData("1234567890a")]      // not numeric
    [InlineData("01234567890")]      // starts with zero
    [InlineData("12345678901")]      // wrong checksum
    public void NationalIdMustBeValid_Rejects_InvalidIds(string nationalId)
    {
        Should.Throw<BusinessException>(() => _rules.NationalIdMustBeValid(nationalId));
    }

    [Fact]
    public void CustomerMustBeAdult_Accepts_ExactlyEighteen()
    {
        Should.NotThrow(() => _rules.CustomerMustBeAdult(new DateOnly(2008, 11, 15)));
    }

    [Fact]
    public void CustomerMustBeAdult_Rejects_OneDayBeforeEighteen()
    {
        Should.Throw<BusinessException>(() => _rules.CustomerMustBeAdult(new DateOnly(2008, 11, 16)));
    }

    [Fact]
    public void CustomerMustBeAdult_FollowsTheClock()
    {
        var bornOnTheSixteenth = new DateOnly(2008, 11, 16);
        Should.Throw<BusinessException>(() => _rules.CustomerMustBeAdult(bornOnTheSixteenth));

        _clock.Advance(TimeSpan.FromDays(1)); // the 18th birthday has come

        Should.NotThrow(() => _rules.CustomerMustBeAdult(bornOnTheSixteenth));
    }

    [Fact]
    public async Task NationalIdCannotBeDuplicated_Throws_WhenAnotherCustomerHasIt()
    {
        _repository.AnyAsync(Arg.Any<Expression<Func<IndividualCustomer, bool>>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await Should.ThrowAsync<BusinessException>(() => _rules.NationalIdCannotBeDuplicated("10000000146", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NationalIdCannotBeDuplicated_ChecksSoftDeletedRowsToo()
    {
        await _rules.NationalIdCannotBeDuplicated("10000000146", cancellationToken: TestContext.Current.CancellationToken);

        await _repository.Received(1).AnyAsync(
            Arg.Any<Expression<Func<IndividualCustomer, bool>>?>(), true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NationalIdCannotBeDuplicated_Predicate_IgnoresTheCustomerBeingUpdated()
    {
        Expression<Func<IndividualCustomer, bool>>? predicate = null;
        _repository.AnyAsync(Arg.Do<Expression<Func<IndividualCustomer, bool>>?>(p => predicate = p), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(false);

        Guid ownId = Guid.NewGuid();
        await _rules.NationalIdCannotBeDuplicated("10000000146", ownId, TestContext.Current.CancellationToken);

        Func<IndividualCustomer, bool> matches = predicate!.Compile();
        matches(new IndividualCustomer { Id = ownId, NationalId = "10000000146" }).ShouldBeFalse();
        matches(new IndividualCustomer { Id = Guid.NewGuid(), NationalId = "10000000146" }).ShouldBeTrue();
        matches(new IndividualCustomer { Id = Guid.NewGuid(), NationalId = "12345678950" }).ShouldBeFalse();
    }
}

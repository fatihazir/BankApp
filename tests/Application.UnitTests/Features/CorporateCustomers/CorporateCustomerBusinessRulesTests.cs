using System.Linq.Expressions;
using Application.Features.CorporateCustomers.Rules;
using Application.Services.Repositories;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Domain.Entities;
using NSubstitute;

namespace Application.UnitTests.Features.CorporateCustomers;

public class CorporateCustomerBusinessRulesTests
{
    private readonly ICorporateCustomerRepository _repository = Substitute.For<ICorporateCustomerRepository>();
    private readonly CorporateCustomerBusinessRules _rules;

    public CorporateCustomerBusinessRulesTests()
    {
        _rules = new CorporateCustomerBusinessRules(_repository);
    }

    [Fact]
    public void CustomerShouldExist_Throws_WhenCustomerIsNull()
    {
        Should.Throw<NotFoundException>(() => _rules.CustomerShouldExist(null));
    }

    [Theory]
    [InlineData("1234567890")]
    [InlineData("0000000001")]
    public void TaxNumberMustBeValid_Accepts_TenDigits(string taxNumber)
    {
        Should.NotThrow(() => _rules.TaxNumberMustBeValid(taxNumber));
    }

    [Theory]
    [InlineData("")]
    [InlineData("123456789")]
    [InlineData("12345678901")]
    [InlineData("12345abcde")]
    public void TaxNumberMustBeValid_Rejects_InvalidValues(string taxNumber)
    {
        Should.Throw<BusinessException>(() => _rules.TaxNumberMustBeValid(taxNumber));
    }

    [Fact]
    public void FoundationDateCannotBeInFuture_Accepts_Today()
    {
        Should.NotThrow(() => _rules.FoundationDateCannotBeInFuture(DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    [Fact]
    public void FoundationDateCannotBeInFuture_Rejects_FutureDate()
    {
        DateOnly tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        Should.Throw<BusinessException>(() => _rules.FoundationDateCannotBeInFuture(tomorrow));
    }

    [Fact]
    public async Task TaxNumberCannotBeDuplicated_Throws_WhenAnotherCustomerHasIt()
    {
        _repository.AnyAsync(Arg.Any<Expression<Func<CorporateCustomer, bool>>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await Should.ThrowAsync<BusinessException>(() => _rules.TaxNumberCannotBeDuplicated("1234567890", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TaxNumberCannotBeDuplicated_Predicate_IgnoresTheCustomerBeingUpdated()
    {
        Expression<Func<CorporateCustomer, bool>>? predicate = null;
        _repository.AnyAsync(Arg.Do<Expression<Func<CorporateCustomer, bool>>?>(p => predicate = p), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(false);

        Guid ownId = Guid.NewGuid();
        await _rules.TaxNumberCannotBeDuplicated("1234567890", ownId, TestContext.Current.CancellationToken);

        Func<CorporateCustomer, bool> matches = predicate!.Compile();
        matches(new CorporateCustomer { Id = ownId, TaxNumber = "1234567890" }).ShouldBeFalse();
        matches(new CorporateCustomer { Id = Guid.NewGuid(), TaxNumber = "1234567890" }).ShouldBeTrue();
    }
}

using System.Linq.Expressions;
using Application.Features.LoanApplications.Queries.Calculate;
using Application.Features.LoanApplications.Rules;
using Application.Services.Repositories;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Domain.Entities;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore.Query;
using NSubstitute;

namespace Application.UnitTests.Features.LoanApplications;

public class CalculateLoanQueryTests
{
    private readonly ILoanSubTypeRepository _subTypes = Substitute.For<ILoanSubTypeRepository>();
    private readonly CalculateLoanQueryHandler _handler;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public CalculateLoanQueryTests()
    {
        _handler = new CalculateLoanQueryHandler(
            _subTypes, new LoanApplicationBusinessRules(Substitute.For<ILoanApplicationRepository>()));
    }

    private void SubTypeIs(LoanSubType? subType) =>
        _subTypes.GetAsync(
                Arg.Any<Expression<Func<LoanSubType, bool>>>(),
                Arg.Any<Func<IQueryable<LoanSubType>, IIncludableQueryable<LoanSubType, object?>>?>(),
                Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(subType);

    private static LoanSubType Personal() => new()
    {
        Id = Guid.NewGuid(), Name = "Personal Loan", MinAmount = 5_000, MaxAmount = 1_000_000,
        MinTermInMonths = 3, MaxTermInMonths = 60, AnnualInterestRate = 12m, IsActive = true,
        LoanType = new LoanType { IsActive = true }
    };

    [Fact]
    public async Task Calculates_WithTheRateOfTheSubType()
    {
        SubTypeIs(Personal());

        var result = await _handler.Handle(new CalculateLoanQuery { LoanSubTypeId = Guid.NewGuid(), Amount = 100_000, TermInMonths = 12 }, Ct);

        result.LoanSubTypeName.ShouldBe("Personal Loan");
        result.AnnualInterestRate.ShouldBe(12m);
        result.MonthlyPayment.ShouldBe(8_884.88m);
        result.Schedule.Count.ShouldBe(12);
        result.Schedule[^1].RemainingBalance.ShouldBe(0m);
        result.TotalRepayment.ShouldBe(result.Schedule.Sum(i => i.Payment));
    }

    [Fact]
    public async Task UnknownSubType_IsNotFound()
    {
        SubTypeIs(null);

        await Should.ThrowAsync<NotFoundException>(() =>
            _handler.Handle(new CalculateLoanQuery { LoanSubTypeId = Guid.NewGuid(), Amount = 10_000, TermInMonths = 12 }, Ct));
    }

    [Theory]
    [InlineData(1_000, 12)]
    [InlineData(2_000_000, 12)]
    [InlineData(10_000, 1)]
    [InlineData(10_000, 100)]
    public async Task OutsideTheLimits_IsRejected(decimal amount, int term)
    {
        SubTypeIs(Personal());

        await Should.ThrowAsync<BusinessException>(() =>
            _handler.Handle(new CalculateLoanQuery { LoanSubTypeId = Guid.NewGuid(), Amount = amount, TermInMonths = term }, Ct));
    }

    [Fact]
    public async Task InactiveSubType_IsRejected()
    {
        var subType = Personal();
        subType.IsActive = false;
        SubTypeIs(subType);

        await Should.ThrowAsync<BusinessException>(() =>
            _handler.Handle(new CalculateLoanQuery { LoanSubTypeId = Guid.NewGuid(), Amount = 10_000, TermInMonths = 12 }, Ct));
    }

    [Fact]
    public void Validator_RequiresIdPositiveAmountAndTerm()
    {
        var validator = new CalculateLoanQueryValidator();

        validator.TestValidate(new CalculateLoanQuery { LoanSubTypeId = Guid.NewGuid(), Amount = 1, TermInMonths = 1 }).ShouldNotHaveAnyValidationErrors();

        var result = validator.TestValidate(new CalculateLoanQuery());
        result.ShouldHaveValidationErrorFor(q => q.LoanSubTypeId);
        result.ShouldHaveValidationErrorFor(q => q.Amount);
        result.ShouldHaveValidationErrorFor(q => q.TermInMonths);
    }
}

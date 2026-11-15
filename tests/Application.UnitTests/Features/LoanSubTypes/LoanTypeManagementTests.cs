using System.Linq.Expressions;
using Application.Features.LoanSubTypes.Commands.Create;
using Application.Features.LoanSubTypes.Profiles;
using Application.Features.LoanSubTypes.Rules;
using Application.Features.LoanTypes.Commands.Create;
using Application.Features.LoanTypes.Profiles;
using Application.Features.LoanTypes.Rules;
using Application.Services.Repositories;
using Application.UnitTests.Support;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Domain.Entities;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore.Query;
using NSubstitute;

namespace Application.UnitTests.Features.LoanSubTypes;

public class LoanTypeManagementTests
{
    private readonly ILoanTypeRepository _loanTypes = Substitute.For<ILoanTypeRepository>();
    private readonly ILoanSubTypeRepository _subTypes = Substitute.For<ILoanSubTypeRepository>();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private void LoanTypeIs(LoanType? loanType) =>
        _loanTypes.GetAsync(
                Arg.Any<Expression<Func<LoanType, bool>>>(),
                Arg.Any<Func<IQueryable<LoanType>, IIncludableQueryable<LoanType, object?>>?>(),
                Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(loanType);

    private static CreateLoanSubTypeCommand ValidSubType() => new()
    {
        LoanTypeId = Guid.NewGuid(), Name = "Education Loan", MinAmount = 10_000, MaxAmount = 500_000,
        MinTermInMonths = 6, MaxTermInMonths = 48, AnnualInterestRate = 39.5m
    };

    // ---- loan type ----

    [Fact]
    public async Task CreateLoanType_SavesAnActiveType_WithATrimmedName()
    {
        var handler = new CreateLoanTypeCommandHandler(_loanTypes, new LoanTypeBusinessRules(_loanTypes));

        var response = await handler.Handle(
            new CreateLoanTypeCommand { Name = "  Special Loan  ", Segment = CustomerSegment.Corporate }, Ct);

        response.Name.ShouldBe("Special Loan");
        response.Segment.ShouldBe(CustomerSegment.Corporate);
        response.IsActive.ShouldBeTrue();
        await _loanTypes.Received(1).AddAsync(
            Arg.Is<LoanType>(t => t.Name == "Special Loan" && t.IsActive), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateLoanType_WithAnExistingName_IsRejected()
    {
        _loanTypes.AnyAsync(Arg.Any<Expression<Func<LoanType, bool>>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(true);
        var handler = new CreateLoanTypeCommandHandler(_loanTypes, new LoanTypeBusinessRules(_loanTypes));

        await Should.ThrowAsync<BusinessException>(() => handler.Handle(new CreateLoanTypeCommand { Name = "X" }, Ct));

        await _loanTypes.DidNotReceiveWithAnyArgs().AddAsync(default!, Ct);
    }

    [Fact]
    public async Task LoanTypeNameCheck_IncludesSoftDeletedRows()
    {
        await new LoanTypeBusinessRules(_loanTypes).NameCannotBeDuplicated("X", Ct);

        await _loanTypes.Received(1).AnyAsync(Arg.Any<Expression<Func<LoanType, bool>>?>(), true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void LoanTypeValidator_ChecksNameDescriptionAndSegment()
    {
        var validator = new CreateLoanTypeCommandValidator();

        validator.TestValidate(new CreateLoanTypeCommand { Name = "X", Segment = CustomerSegment.Individual }).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new CreateLoanTypeCommand { Name = "", Segment = CustomerSegment.Individual }).ShouldHaveValidationErrorFor(c => c.Name);
        validator.TestValidate(new CreateLoanTypeCommand { Name = new string('x', 101), Segment = CustomerSegment.Individual }).ShouldHaveValidationErrorFor(c => c.Name);
        validator.TestValidate(new CreateLoanTypeCommand { Name = "X", Description = new string('x', 501), Segment = CustomerSegment.Individual }).ShouldHaveValidationErrorFor(c => c.Description);
        validator.TestValidate(new CreateLoanTypeCommand { Name = "X" }).ShouldHaveValidationErrorFor(c => c.Segment);
    }

    [Fact]
    public void LoanTypeRules_ChecksExistenceAndActivity()
    {
        var rules = new LoanTypeBusinessRules(_loanTypes);

        Should.Throw<NotFoundException>(() => rules.LoanTypeShouldExist(null));
        Should.Throw<BusinessException>(() => rules.LoanTypeMustBeActive(new LoanType { IsActive = false }));
        Should.NotThrow(() => rules.LoanTypeMustBeActive(new LoanType { IsActive = true }));
    }

    [Fact]
    public void LoanTypeMapper_CopiesEveryCommandPropertyAndLeavesSystemFieldsUntouched()
    {
        var command = MappingContract.Filled<CreateLoanTypeCommand>();

        LoanType entity = LoanTypeMapper.ToEntity(command);

        MappingContract.AssertAllSourcePropertiesCopied(command, entity);
        entity.Id.ShouldBe(Guid.Empty);
        entity.CreateDate.ShouldBe(default);
        MappingContract.AssertEntityPropertiesAreAccountedFor(
            typeof(LoanType), typeof(CreateLoanTypeCommand),
            [nameof(LoanType.Id), nameof(LoanType.IsActive), nameof(LoanType.SubTypes),
             nameof(LoanType.CreateDate), nameof(LoanType.UpdateDate), nameof(LoanType.DeleteDate)]);
    }

    // ---- loan sub type ----

    private CreateLoanSubTypeCommandHandler SubTypeHandler() => new(
        _loanTypes, _subTypes, new LoanTypeBusinessRules(_loanTypes), new LoanSubTypeBusinessRules(_subTypes));

    [Fact]
    public async Task CreateSubType_SavesAnActiveSubType_UnderTheGivenType()
    {
        LoanTypeIs(new LoanType { IsActive = true });
        var command = ValidSubType();

        var response = await SubTypeHandler().Handle(command, Ct);

        response.LoanTypeId.ShouldBe(command.LoanTypeId);
        response.IsActive.ShouldBeTrue();
        response.AnnualInterestRate.ShouldBe(39.5m);
        await _subTypes.Received(1).AddAsync(
            Arg.Is<LoanSubType>(s => s.LoanTypeId == command.LoanTypeId && s.IsActive && s.MaxTermInMonths == 48),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateSubType_UnderAnUnknownType_IsNotFound()
    {
        LoanTypeIs(null);

        await Should.ThrowAsync<NotFoundException>(() => SubTypeHandler().Handle(ValidSubType(), Ct));
    }

    [Fact]
    public async Task CreateSubType_UnderAnInactiveType_IsRejected()
    {
        LoanTypeIs(new LoanType { IsActive = false });

        await Should.ThrowAsync<BusinessException>(() => SubTypeHandler().Handle(ValidSubType(), Ct));
        await _subTypes.DidNotReceiveWithAnyArgs().AddAsync(default!, Ct);
    }

    [Fact]
    public async Task CreateSubType_WithADuplicateNameInTheSameType_IsRejected()
    {
        LoanTypeIs(new LoanType { IsActive = true });
        _subTypes.AnyAsync(Arg.Any<Expression<Func<LoanSubType, bool>>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(true);

        await Should.ThrowAsync<BusinessException>(() => SubTypeHandler().Handle(ValidSubType(), Ct));
    }

    [Fact]
    public async Task SubTypeNameCheck_OnlyMatchesTheSameTypeAndName_IncludingSoftDeletedRows()
    {
        Expression<Func<LoanSubType, bool>>? predicate = null;
        _subTypes.AnyAsync(Arg.Do<Expression<Func<LoanSubType, bool>>?>(p => predicate = p), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(false);
        Guid typeId = Guid.NewGuid();

        await new LoanSubTypeBusinessRules(_subTypes).NameCannotBeDuplicatedWithinLoanType(typeId, "Auto", Ct);

        await _subTypes.Received(1).AnyAsync(Arg.Any<Expression<Func<LoanSubType, bool>>?>(), true, Arg.Any<CancellationToken>());
        var matches = predicate!.Compile();
        matches(new LoanSubType { LoanTypeId = typeId, Name = "Auto" }).ShouldBeTrue();
        matches(new LoanSubType { LoanTypeId = typeId, Name = "Other" }).ShouldBeFalse();
        matches(new LoanSubType { LoanTypeId = Guid.NewGuid(), Name = "Auto" }).ShouldBeFalse();
    }

    [Fact]
    public void SubTypeValidator_AcceptsAValidCommand() =>
        new CreateLoanSubTypeCommandValidator().TestValidate(ValidSubType()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void SubTypeValidator_RejectsInconsistentLimits()
    {
        var validator = new CreateLoanSubTypeCommandValidator();

        var maxBelowMin = ValidSubType();
        maxBelowMin.MaxAmount = maxBelowMin.MinAmount - 1;
        validator.TestValidate(maxBelowMin).ShouldHaveValidationErrorFor(c => c.MaxAmount);

        var termBelowMin = ValidSubType();
        termBelowMin.MaxTermInMonths = termBelowMin.MinTermInMonths - 1;
        validator.TestValidate(termBelowMin).ShouldHaveValidationErrorFor(c => c.MaxTermInMonths);

        var zeroMin = ValidSubType();
        zeroMin.MinAmount = 0;
        validator.TestValidate(zeroMin).ShouldHaveValidationErrorFor(c => c.MinAmount);

        var zeroTerm = ValidSubType();
        zeroTerm.MinTermInMonths = 0;
        validator.TestValidate(zeroTerm).ShouldHaveValidationErrorFor(c => c.MinTermInMonths);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    [InlineData(12.345)]
    public void SubTypeValidator_RejectsAnInvalidRate(double rate)
    {
        var command = ValidSubType();
        command.AnnualInterestRate = (decimal)rate;

        new CreateLoanSubTypeCommandValidator().TestValidate(command).ShouldHaveValidationErrorFor(c => c.AnnualInterestRate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(42.5)]
    public void SubTypeValidator_AcceptsRatesFromZeroToHundred(double rate)
    {
        var command = ValidSubType();
        command.AnnualInterestRate = (decimal)rate;

        new CreateLoanSubTypeCommandValidator().TestValidate(command).ShouldNotHaveValidationErrorFor(c => c.AnnualInterestRate);
    }

    [Fact]
    public void SubTypeValidator_RequiresTheNameAndLoanType()
    {
        var result = new CreateLoanSubTypeCommandValidator().TestValidate(new CreateLoanSubTypeCommand());

        result.ShouldHaveValidationErrorFor(c => c.LoanTypeId);
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void SubTypeMapper_CopiesEveryCommandPropertyAndLeavesSystemFieldsUntouched()
    {
        var command = MappingContract.Filled<CreateLoanSubTypeCommand>();

        LoanSubType entity = LoanSubTypeMapper.ToEntity(command);

        MappingContract.AssertAllSourcePropertiesCopied(command, entity);
        entity.Id.ShouldBe(Guid.Empty);
        MappingContract.AssertEntityPropertiesAreAccountedFor(
            typeof(LoanSubType), typeof(CreateLoanSubTypeCommand),
            [nameof(LoanSubType.Id), nameof(LoanSubType.LoanType), nameof(LoanSubType.IsActive),
             nameof(LoanSubType.CreateDate), nameof(LoanSubType.UpdateDate), nameof(LoanSubType.DeleteDate)]);
    }
}

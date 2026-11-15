using Application.Features.LoanApplications.Commands.Approve;
using Application.Features.LoanApplications.Commands.Cancel;
using Application.Features.LoanApplications.Commands.Create;
using Application.Features.LoanApplications.Commands.Reject;
using Application.Features.LoanApplications.Commands.StartReview;
using Application.Features.LoanApplications.Queries.GetById;
using Application.Features.LoanApplications.Queries.GetList;
using Application.Features.LoanTypes.Queries.GetList;
using Domain.Entities;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Features.LoanApplications;

public class LoanApplicationValidatorTests
{
    private static CreateLoanApplicationCommand ValidCreate() => new()
    {
        CustomerId = Guid.NewGuid(), LoanSubTypeId = Guid.NewGuid(), RequestedAmount = 10_000, TermInMonths = 12
    };

    [Fact]
    public void Create_Valid_HasNoErrors() =>
        new CreateLoanApplicationCommandValidator().TestValidate(ValidCreate()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Create_Empty_HasErrorsOnRequiredFields()
    {
        var result = new CreateLoanApplicationCommandValidator().TestValidate(new CreateLoanApplicationCommand());

        result.ShouldHaveValidationErrorFor(c => c.CustomerId);
        result.ShouldHaveValidationErrorFor(c => c.LoanSubTypeId);
        result.ShouldHaveValidationErrorFor(c => c.RequestedAmount);
        result.ShouldHaveValidationErrorFor(c => c.TermInMonths);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveAmountOrTerm_HasErrors(int value)
    {
        var command = ValidCreate();
        command.RequestedAmount = value;
        command.TermInMonths = value;

        var result = new CreateLoanApplicationCommandValidator().TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.RequestedAmount);
        result.ShouldHaveValidationErrorFor(c => c.TermInMonths);
    }

    [Fact]
    public void Create_TooLongPurpose_HasError()
    {
        var command = ValidCreate();
        command.Purpose = new string('x', 501);
        new CreateLoanApplicationCommandValidator().TestValidate(command).ShouldHaveValidationErrorFor(c => c.Purpose);
    }

    [Fact]
    public void Approve_RequiresIdAndPositiveAmount()
    {
        var validator = new ApproveLoanApplicationCommandValidator();

        validator.TestValidate(new ApproveLoanApplicationCommand { Id = Guid.NewGuid(), ApprovedAmount = 1 }).ShouldNotHaveAnyValidationErrors();

        var result = validator.TestValidate(new ApproveLoanApplicationCommand());
        result.ShouldHaveValidationErrorFor(c => c.Id);
        result.ShouldHaveValidationErrorFor(c => c.ApprovedAmount);
    }

    [Fact]
    public void Reject_RequiresAReason()
    {
        var validator = new RejectLoanApplicationCommandValidator();

        validator.TestValidate(new RejectLoanApplicationCommand { Id = Guid.NewGuid(), Reason = "Too risky" }).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new RejectLoanApplicationCommand { Id = Guid.NewGuid(), Reason = " " }).ShouldHaveValidationErrorFor(c => c.Reason);
        validator.TestValidate(new RejectLoanApplicationCommand { Id = Guid.NewGuid(), Reason = new string('x', 1001) }).ShouldHaveValidationErrorFor(c => c.Reason);
    }

    [Fact]
    public void Cancel_ReasonIsOptional_ButLimitedInLength()
    {
        var validator = new CancelLoanApplicationCommandValidator();

        validator.TestValidate(new CancelLoanApplicationCommand { Id = Guid.NewGuid() }).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new CancelLoanApplicationCommand { Id = Guid.NewGuid(), Reason = new string('x', 1001) }).ShouldHaveValidationErrorFor(c => c.Reason);
    }

    [Fact]
    public void IdOnlyRequests_RejectAnEmptyId()
    {
        new StartReviewLoanApplicationCommandValidator().TestValidate(new StartReviewLoanApplicationCommand()).ShouldHaveValidationErrorFor(c => c.Id);
        new GetByIdLoanApplicationQueryValidator().TestValidate(new GetByIdLoanApplicationQuery()).ShouldHaveValidationErrorFor(q => q.Id);
    }

    [Theory]
    [InlineData(-1, 10, false)]
    [InlineData(0, 0, false)]
    [InlineData(0, 101, false)]
    [InlineData(0, 1, true)]
    [InlineData(5, 100, true)]
    public void GetList_ChecksPaging(int index, int size, bool valid)
    {
        var result = new GetListLoanApplicationQueryValidator().TestValidate(new GetListLoanApplicationQuery { Index = index, Size = size });

        if (valid) result.ShouldNotHaveAnyValidationErrors();
        else result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void GetList_InvalidStatusValue_HasError() =>
        new GetListLoanApplicationQueryValidator()
            .TestValidate(new GetListLoanApplicationQuery { Status = (LoanApplicationStatus)99 })
            .ShouldHaveValidationErrorFor(q => q.Status);

    [Fact]
    public void LoanTypes_InvalidSegment_HasError()
    {
        var validator = new GetListLoanTypeQueryValidator();

        validator.TestValidate(new GetListLoanTypeQuery { Segment = CustomerSegment.Corporate }).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new GetListLoanTypeQuery()).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new GetListLoanTypeQuery { Segment = (CustomerSegment)99 }).ShouldHaveValidationErrorFor(q => q.Segment);
    }
}

using FluentValidation;

namespace Application.Features.LoanSubTypes.Commands.Create;

public class CreateLoanSubTypeCommandValidator : AbstractValidator<CreateLoanSubTypeCommand>
{
    public CreateLoanSubTypeCommandValidator()
    {
        RuleFor(c => c.LoanTypeId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Description).MaximumLength(500);

        RuleFor(c => c.MinAmount).GreaterThan(0).PrecisionScale(18, 2, false);
        RuleFor(c => c.MaxAmount).GreaterThanOrEqualTo(c => c.MinAmount).PrecisionScale(18, 2, false);

        RuleFor(c => c.MinTermInMonths).GreaterThanOrEqualTo(1);
        RuleFor(c => c.MaxTermInMonths).GreaterThanOrEqualTo(c => c.MinTermInMonths);

        RuleFor(c => c.AnnualInterestRate).InclusiveBetween(0m, 100m).PrecisionScale(5, 2, false);
    }
}

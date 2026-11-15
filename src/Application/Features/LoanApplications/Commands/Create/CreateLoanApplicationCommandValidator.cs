using FluentValidation;

namespace Application.Features.LoanApplications.Commands.Create;

public class CreateLoanApplicationCommandValidator : AbstractValidator<CreateLoanApplicationCommand>
{
    public CreateLoanApplicationCommandValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.LoanSubTypeId).NotEmpty();
        RuleFor(c => c.RequestedAmount).GreaterThan(0);
        RuleFor(c => c.TermInMonths).GreaterThan(0);
        RuleFor(c => c.Purpose).MaximumLength(500);
    }
}

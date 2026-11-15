using FluentValidation;

namespace Application.Features.LoanApplications.Commands.Cancel;

public class CancelLoanApplicationCommandValidator : AbstractValidator<CancelLoanApplicationCommand>
{
    public CancelLoanApplicationCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Reason).MaximumLength(1000);
    }
}

using FluentValidation;

namespace Application.Features.LoanApplications.Commands.Reject;

public class RejectLoanApplicationCommandValidator : AbstractValidator<RejectLoanApplicationCommand>
{
    public RejectLoanApplicationCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(1000);
    }
}

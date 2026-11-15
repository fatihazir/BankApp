using FluentValidation;

namespace Application.Features.LoanApplications.Commands.Approve;

public class ApproveLoanApplicationCommandValidator : AbstractValidator<ApproveLoanApplicationCommand>
{
    public ApproveLoanApplicationCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.ApprovedAmount).GreaterThan(0);
        RuleFor(c => c.Reason).MaximumLength(1000);
    }
}

using FluentValidation;

namespace Application.Features.LoanApplications.Commands.StartReview;

public class StartReviewLoanApplicationCommandValidator : AbstractValidator<StartReviewLoanApplicationCommand>
{
    public StartReviewLoanApplicationCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

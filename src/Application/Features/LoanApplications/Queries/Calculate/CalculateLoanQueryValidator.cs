using FluentValidation;

namespace Application.Features.LoanApplications.Queries.Calculate;

public class CalculateLoanQueryValidator : AbstractValidator<CalculateLoanQuery>
{
    public CalculateLoanQueryValidator()
    {
        RuleFor(q => q.LoanSubTypeId).NotEmpty();
        RuleFor(q => q.Amount).GreaterThan(0);
        RuleFor(q => q.TermInMonths).GreaterThan(0);
    }
}

using FluentValidation;

namespace Application.Features.LoanApplications.Queries.GetList;

public class GetListLoanApplicationQueryValidator : AbstractValidator<GetListLoanApplicationQuery>
{
    public GetListLoanApplicationQueryValidator()
    {
        RuleFor(q => q.Index).GreaterThanOrEqualTo(0);
        RuleFor(q => q.Size).InclusiveBetween(1, 100);
        RuleFor(q => q.Status).IsInEnum().When(q => q.Status.HasValue);
    }
}

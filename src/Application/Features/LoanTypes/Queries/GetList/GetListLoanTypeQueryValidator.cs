using FluentValidation;

namespace Application.Features.LoanTypes.Queries.GetList;

public class GetListLoanTypeQueryValidator : AbstractValidator<GetListLoanTypeQuery>
{
    public GetListLoanTypeQueryValidator()
    {
        RuleFor(q => q.Index).GreaterThanOrEqualTo(0);
        RuleFor(q => q.Size).InclusiveBetween(1, 100);
        RuleFor(q => q.Segment).IsInEnum().When(q => q.Segment.HasValue);
    }
}

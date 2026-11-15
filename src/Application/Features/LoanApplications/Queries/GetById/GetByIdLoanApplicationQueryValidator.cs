using FluentValidation;

namespace Application.Features.LoanApplications.Queries.GetById;

public class GetByIdLoanApplicationQueryValidator : AbstractValidator<GetByIdLoanApplicationQuery>
{
    public GetByIdLoanApplicationQueryValidator()
    {
        RuleFor(q => q.Id).NotEmpty();
    }
}

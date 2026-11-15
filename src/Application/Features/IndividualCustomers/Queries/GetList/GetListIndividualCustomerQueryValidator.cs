using FluentValidation;

namespace Application.Features.IndividualCustomers.Queries.GetList;

public class GetListIndividualCustomerQueryValidator : AbstractValidator<GetListIndividualCustomerQuery>
{
    public GetListIndividualCustomerQueryValidator()
    {
        RuleFor(q => q.Index).GreaterThanOrEqualTo(0);
        RuleFor(q => q.Size).InclusiveBetween(1, 100);
    }
}

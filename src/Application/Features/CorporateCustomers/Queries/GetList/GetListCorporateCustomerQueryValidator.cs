using FluentValidation;

namespace Application.Features.CorporateCustomers.Queries.GetList;

public class GetListCorporateCustomerQueryValidator : AbstractValidator<GetListCorporateCustomerQuery>
{
    public GetListCorporateCustomerQueryValidator()
    {
        RuleFor(q => q.Index).GreaterThanOrEqualTo(0);
        RuleFor(q => q.Size).InclusiveBetween(1, 100);
    }
}

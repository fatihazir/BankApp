using FluentValidation;

namespace Application.Features.CorporateCustomers.Queries.GetById;

public class GetByIdCorporateCustomerQueryValidator : AbstractValidator<GetByIdCorporateCustomerQuery>
{
    public GetByIdCorporateCustomerQueryValidator()
    {
        RuleFor(q => q.Id).NotEmpty();
    }
}

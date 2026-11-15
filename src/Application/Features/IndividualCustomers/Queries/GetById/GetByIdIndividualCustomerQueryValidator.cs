using FluentValidation;

namespace Application.Features.IndividualCustomers.Queries.GetById;

public class GetByIdIndividualCustomerQueryValidator : AbstractValidator<GetByIdIndividualCustomerQuery>
{
    public GetByIdIndividualCustomerQueryValidator()
    {
        RuleFor(q => q.Id).NotEmpty();
    }
}

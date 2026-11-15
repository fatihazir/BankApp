using FluentValidation;

namespace Application.Features.CorporateCustomers.Commands.Update;

public class UpdateCorporateCustomerCommandValidator : AbstractValidator<UpdateCorporateCustomerCommand>
{
    public UpdateCorporateCustomerCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();

        RuleFor(c => c.PhoneNumber).NotEmpty().MaximumLength(20);
        RuleFor(c => c.Address).NotEmpty().MaximumLength(500);
        RuleFor(c => c.City).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Country).NotEmpty().MaximumLength(100);
        RuleFor(c => c.PostalCode).MaximumLength(10);

        RuleFor(c => c.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.TaxNumber).NotEmpty().Length(10);
        RuleFor(c => c.TaxOffice).NotEmpty().MaximumLength(100);
        RuleFor(c => c.TradeRegistryNumber).MaximumLength(50);
        RuleFor(c => c.FoundationDate).NotEmpty();
        RuleFor(c => c.Sector).MaximumLength(100);
        RuleFor(c => c.EmployeeCount).GreaterThanOrEqualTo(0);
        RuleFor(c => c.AnnualRevenue).GreaterThanOrEqualTo(0);
        RuleFor(c => c.AuthorizedPersonName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.AuthorizedPersonTitle).MaximumLength(100);
        RuleFor(c => c.AuthorizedPersonNationalId).Length(11).When(c => !string.IsNullOrEmpty(c.AuthorizedPersonNationalId));
    }
}

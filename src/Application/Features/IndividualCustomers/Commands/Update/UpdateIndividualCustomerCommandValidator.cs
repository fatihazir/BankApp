using FluentValidation;

namespace Application.Features.IndividualCustomers.Commands.Update;

public class UpdateIndividualCustomerCommandValidator : AbstractValidator<UpdateIndividualCustomerCommand>
{
    public UpdateIndividualCustomerCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();

        RuleFor(c => c.PhoneNumber).NotEmpty().MaximumLength(20);
        RuleFor(c => c.Address).NotEmpty().MaximumLength(500);
        RuleFor(c => c.City).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Country).NotEmpty().MaximumLength(100);
        RuleFor(c => c.PostalCode).MaximumLength(10);

        RuleFor(c => c.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(c => c.LastName).NotEmpty().MaximumLength(100);
        RuleFor(c => c.NationalId).NotEmpty().Length(11);
        RuleFor(c => c.DateOfBirth).NotEmpty();
        RuleFor(c => c.MotherName).MaximumLength(100);
        RuleFor(c => c.EmploymentStatus).IsInEnum();
        RuleFor(c => c.Occupation).MaximumLength(100);
        RuleFor(c => c.EmployerName).MaximumLength(200);
        RuleFor(c => c.MonthlyIncome).GreaterThanOrEqualTo(0);
    }
}

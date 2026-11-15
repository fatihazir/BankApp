using FluentValidation;

namespace Application.Features.LoanTypes.Commands.Create;

public class CreateLoanTypeCommandValidator : AbstractValidator<CreateLoanTypeCommand>
{
    public CreateLoanTypeCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Description).MaximumLength(500);
        RuleFor(c => c.Segment).IsInEnum();
    }
}

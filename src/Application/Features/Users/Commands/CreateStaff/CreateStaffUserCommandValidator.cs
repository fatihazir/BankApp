using Application.Services.Security;
using FluentValidation;

namespace Application.Features.Users.Commands.CreateStaff;

public class CreateStaffUserCommandValidator : AbstractValidator<CreateStaffUserCommand>
{
    public CreateStaffUserCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(c => c.Password).MustBeAStrongPassword();
    }
}

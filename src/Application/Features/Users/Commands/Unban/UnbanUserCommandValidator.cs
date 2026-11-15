using FluentValidation;

namespace Application.Features.Users.Commands.Unban;

public class UnbanUserCommandValidator : AbstractValidator<UnbanUserCommand>
{
    public UnbanUserCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

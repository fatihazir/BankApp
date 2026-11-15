using FluentValidation;

namespace Application.Services.Security;

public static class PasswordRuleExtensions
{
    /// <summary>At least 10 characters with an upper case letter, a lower case letter and a digit.</summary>
    public static IRuleBuilderOptions<T, string> MustBeAStrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MinimumLength(10).WithMessage("'{PropertyName}' must be at least 10 characters long.")
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("'{PropertyName}' must contain an upper case letter.")
            .Matches("[a-z]").WithMessage("'{PropertyName}' must contain a lower case letter.")
            .Matches("[0-9]").WithMessage("'{PropertyName}' must contain a digit.");
}

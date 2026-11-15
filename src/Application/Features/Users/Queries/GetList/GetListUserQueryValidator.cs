using FluentValidation;

namespace Application.Features.Users.Queries.GetList;

public class GetListUserQueryValidator : AbstractValidator<GetListUserQuery>
{
    public GetListUserQueryValidator()
    {
        RuleFor(q => q.Index).GreaterThanOrEqualTo(0);
        RuleFor(q => q.Size).InclusiveBetween(1, 100);
        RuleFor(q => q.Role).IsInEnum().When(q => q.Role.HasValue);
    }
}

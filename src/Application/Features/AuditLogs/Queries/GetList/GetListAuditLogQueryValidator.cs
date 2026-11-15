using FluentValidation;

namespace Application.Features.AuditLogs.Queries.GetList;

public class GetListAuditLogQueryValidator : AbstractValidator<GetListAuditLogQuery>
{
    public GetListAuditLogQueryValidator()
    {
        RuleFor(q => q.Index).GreaterThanOrEqualTo(0);
        RuleFor(q => q.Size).InclusiveBetween(1, 100);
        RuleFor(q => q.Action).MaximumLength(100);
        RuleFor(q => q.To).GreaterThan(q => q.From).When(q => q.From.HasValue && q.To.HasValue)
            .WithMessage("'To' must be later than 'From'.");
    }
}

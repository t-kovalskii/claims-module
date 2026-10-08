using FluentValidation;

namespace ClaimsModule.Application.Claims.Queries.GetClaimAuditLog;

public class GetClaimAuditLogQueryValidator : AbstractValidator<GetClaimAuditLogQuery>
{
    public GetClaimAuditLogQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

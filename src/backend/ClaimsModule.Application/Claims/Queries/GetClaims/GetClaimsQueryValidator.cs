using FluentValidation;

namespace ClaimsModule.Application.Claims.Queries.GetClaims;

public class GetClaimsQueryValidator : AbstractValidator<GetClaimsQuery>
{
    public GetClaimsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

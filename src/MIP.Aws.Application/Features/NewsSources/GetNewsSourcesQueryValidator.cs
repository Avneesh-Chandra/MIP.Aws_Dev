using FluentValidation;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed class GetNewsSourcesQueryValidator : AbstractValidator<GetNewsSourcesQuery>
{
    public GetNewsSourcesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

using FluentValidation;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed class TestNewsSourceLoginCommandValidator : AbstractValidator<TestNewsSourceLoginCommand>
{
    public TestNewsSourceLoginCommandValidator()
    {
        RuleFor(x => x.NewsSourceId).NotEmpty();
    }
}

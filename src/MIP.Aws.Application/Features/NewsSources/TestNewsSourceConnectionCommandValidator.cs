using FluentValidation;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed class TestNewsSourceConnectionCommandValidator : AbstractValidator<TestNewsSourceConnectionCommand>
{
    public TestNewsSourceConnectionCommandValidator()
    {
        RuleFor(x => x.BaseUrl).NotEmpty().MaximumLength(2048).Must(BeHttp).WithMessage("BaseUrl must be an absolute http(s) URL.");
    }

    private static bool BeHttp(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);
}

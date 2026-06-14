using FluentValidation;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed class DownloadNewsSourceEditionCommandValidator : AbstractValidator<DownloadNewsSourceEditionCommand>
{
    public DownloadNewsSourceEditionCommandValidator()
    {
        RuleFor(x => x.NewsSourceId).NotEmpty();
    }
}

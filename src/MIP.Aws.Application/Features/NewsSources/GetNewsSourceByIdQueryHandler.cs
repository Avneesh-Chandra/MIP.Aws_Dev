using MIP.Aws.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed class GetNewsSourceByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetNewsSourceByIdQuery, NewsSourceDetailDto>
{
    public async Task<NewsSourceDetailDto> Handle(GetNewsSourceByIdQuery request, CancellationToken cancellationToken)
    {
        var x = await db.NewsSources.AsNoTracking()
            .Include(s => s.SourceCategory)
            .Include(s => s.DownloadSchedule)
            .Include(s => s.Credential)
            .FirstOrDefaultAsync(s => s.Id == request.Id && !s.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        if (x is null)
        {
            throw new InvalidOperationException("News source was not found.");
        }

        var hasPw = x.Credential is not null && !string.IsNullOrEmpty(x.Credential.ProtectedCredentialPayload);

        return new NewsSourceDetailDto(
            x.Id,
            x.Name,
            x.BaseUrl,
            x.SourceType,
            x.AcquisitionMode,
            x.SourceAccessMode,
            x.RequiresLogin,
            x.LoginUrl,
            x.EditionUrl,
            x.LogoutUrl,
            x.PortalUsername,
            hasPw,
            x.LoginMethod,
            x.UsernameSelector,
            x.PasswordSelector,
            x.SubmitSelector,
            x.DownloadSelector,
            x.LoginSuccessSelector,
            x.SuccessUrlPattern,
            x.RequiresCaptcha,
            x.IsDownloadAllowed,
            x.RequiresManualAction,
            x.RequiresMfa,
            x.RequiresOtp,
            x.ManualLoginRequired,
            x.OtpInstructions,
            x.AssistedSessionTimeoutMinutes,
            x.Notes,
            x.DefaultLanguage,
            x.Country,
            x.RequiresAuthentication,
            x.UseHeadlessBrowser,
            x.DownloadFrequencyMinutes,
            x.ConnectorKey,
            x.SourceCategoryId,
            x.SourceCategory?.Name,
            x.IsEnabled,
            x.LastDownloadAt,
            x.DownloadSchedule?.CronExpression,
            x.DownloadSchedule?.TimeZoneId,
            x.DownloadSchedule?.IsEnabled ?? false,
            PdfDiscoveryFieldMapper.ToDto(x),
            PortalFieldMapper.ToDto(x));
    }
}

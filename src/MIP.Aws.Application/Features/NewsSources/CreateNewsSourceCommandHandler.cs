using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Security;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed class CreateNewsSourceCommandHandler(IApplicationDbContext db, INewsCredentialProtector credentialProtector)
    : IRequestHandler<CreateNewsSourceCommand, Guid>
{
    public async Task<Guid> Handle(CreateNewsSourceCommand request, CancellationToken cancellationToken)
    {
        var portalStrategyKey = request.Portal?.PortalStrategyKey;
        await NewsSourceUrlRules.EnsureUniqueAsync(
                db,
                excludeId: null,
                request.SourceType,
                request.BaseUrl,
                request.EditionUrl,
                portalStrategyKey,
                cancellationToken)
            .ConfigureAwait(false);

        var normalized = NewsSourceUrlRules.ResolveBaseUrl(
            request.SourceType,
            request.BaseUrl,
            request.EditionUrl,
            portalStrategyKey);

        var isManualAssisted = request.ManualLoginRequired
            || request.RequiresMfa
            || request.RequiresOtp
            || request.LoginMethod is PortalLoginMethod.ManualOtpAssisted or PortalLoginMethod.ManualBrowserSession;

        var entity = new NewsSource
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            BaseUrl = normalized,
            SourceType = request.SourceType,
            AcquisitionMode = request.AcquisitionMode,
            SourceAccessMode = request.SourceAccessMode != NewsSourceAccessMode.Unspecified
                ? request.SourceAccessMode
                : (request.SourceType == NewsSourceType.WebPortalLogin
                    ? NewsSourceAccessMode.LicensedSubscriberPortal
                    : NewsSourceAccessMode.PublicWeb),
            RequiresLogin = request.RequiresLogin || request.SourceType == NewsSourceType.WebPortalLogin,
            LoginUrl = request.LoginUrl?.Trim(),
            EditionUrl = request.EditionUrl?.Trim(),
            LogoutUrl = request.LogoutUrl?.Trim(),
            PortalUsername = request.PortalUsername?.Trim(),
            LoginMethod = request.LoginMethod,
            UsernameSelector = request.UsernameSelector?.Trim(),
            PasswordSelector = request.PasswordSelector?.Trim(),
            SubmitSelector = request.SubmitSelector?.Trim(),
            DownloadSelector = request.DownloadSelector?.Trim(),
            LoginSuccessSelector = request.LoginSuccessSelector?.Trim(),
            SuccessUrlPattern = request.SuccessUrlPattern?.Trim(),
            RequiresCaptcha = request.RequiresCaptcha,
            IsDownloadAllowed = request.IsDownloadAllowed,
            RequiresManualAction = isManualAssisted,
            RequiresMfa = request.RequiresMfa,
            RequiresOtp = request.RequiresOtp,
            ManualLoginRequired = request.ManualLoginRequired || isManualAssisted,
            OtpInstructions = request.OtpInstructions?.Trim(),
            AssistedSessionTimeoutMinutes = isManualAssisted ? (request.AssistedSessionTimeoutMinutes ?? 30) : request.AssistedSessionTimeoutMinutes,
            Notes = request.Notes?.Trim(),
            DefaultLanguage = request.DefaultLanguage,
            Country = request.Country,
            RequiresAuthentication = request.RequiresAuthentication,
            UseHeadlessBrowser = request.UseHeadlessBrowser,
            DownloadFrequencyMinutes = request.DownloadFrequencyMinutes,
            ConnectorKey = request.ConnectorKey,
            SourceCategoryId = request.SourceCategoryId,
            IsEnabled = request.IsEnabled,
            CreatedAt = DateTimeOffset.UtcNow
        };

        if (request.PdfDiscovery is { } pdf)
        {
            PdfDiscoveryFieldMapper.Apply(entity, pdf);
        }

        if (request.Portal is { } portal)
        {
            PortalFieldMapper.Apply(entity, portal);
        }

        db.NewsSources.Add(entity);

        if (!string.IsNullOrWhiteSpace(request.CronExpression))
        {
            db.DownloadSchedules.Add(new DownloadSchedule
            {
                Id = Guid.NewGuid(),
                NewsSourceId = entity.Id,
                CronExpression = request.CronExpression.Trim(),
                TimeZoneId = string.IsNullOrWhiteSpace(request.ScheduleTimeZoneId) ? "UTC" : request.ScheduleTimeZoneId.Trim(),
                IsEnabled = request.ScheduleEnabled,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        var userForProtect = !string.IsNullOrWhiteSpace(request.PortalUsername)
            ? request.PortalUsername.Trim()
            : request.CredentialUsername?.Trim();
        if (!isManualAssisted &&
            !string.IsNullOrWhiteSpace(request.CredentialPassword) &&
            !string.IsNullOrWhiteSpace(userForProtect) &&
            (request.RequiresAuthentication || request.SourceType == NewsSourceType.WebPortalLogin))
        {
            var payload = credentialProtector.Protect(userForProtect, request.CredentialPassword);
            db.SourceCredentials.Add(new SourceCredential
            {
                Id = Guid.NewGuid(),
                NewsSourceId = entity.Id,
                ProtectedCredentialPayload = payload,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Id;
    }
}

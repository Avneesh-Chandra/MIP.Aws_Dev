using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Security;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed class UpdateNewsSourceCommandHandler(IApplicationDbContext db, INewsCredentialProtector credentialProtector)
    : IRequestHandler<UpdateNewsSourceCommand, Unit>
{
    public async Task<Unit> Handle(UpdateNewsSourceCommand request, CancellationToken cancellationToken)
    {
        var portalProbe = await db.NewsSources.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.Id && !s.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        if (portalProbe is null)
        {
            throw new InvalidOperationException("News source was not found.");
        }

        var portalStrategyKey = request.Portal?.PortalStrategyKey ?? portalProbe.PortalStrategyKey;
        var editionUrl = string.IsNullOrWhiteSpace(request.EditionUrl) ? portalProbe.EditionUrl : request.EditionUrl;
        var baseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? portalProbe.BaseUrl : request.BaseUrl;

        if (NewsSourceUrlRules.IdentityChanged(
                portalProbe.SourceType,
                portalProbe.BaseUrl,
                portalProbe.EditionUrl,
                portalProbe.PortalStrategyKey,
                request.SourceType,
                baseUrl,
                editionUrl,
                portalStrategyKey))
        {
            await NewsSourceUrlRules.EnsureUniqueAsync(
                    db,
                    request.Id,
                    request.SourceType,
                    baseUrl,
                    editionUrl,
                    portalStrategyKey,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var entity = await LoadTrackedSourceAsync(request.Id, cancellationToken).ConfigureAwait(false);
            if (entity is null)
            {
                throw new InvalidOperationException("News source was not found.");
            }

            ApplyRequest(db, entity, request, credentialProtector);

            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await DarAlKhaleejPressReaderCredentialSync.MirrorToSiblingEditionAsync(
                        db,
                        entity,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (db is DbContext mirrorContext && mirrorContext.ChangeTracker.HasChanges())
                {
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                return Unit.Value;
            }
            catch (DbUpdateConcurrencyException) when (attempt < 2)
            {
                // Another process (PDF batch, auto-recovery, catalog seed) updated the same row — reload and retry.
            }
        }

        throw new InvalidOperationException(
            "The source was updated by another process (for example a running PDF download or auto-recovery). Refresh the editor and try again.");
    }

    private async Task<NewsSource?> LoadTrackedSourceAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (db is DbContext context)
        {
            context.ChangeTracker.Clear();
        }

        return await db.NewsSources
            .Include(s => s.Credential)
            .Include(s => s.DownloadSchedule)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
    }

    private static void ApplyRequest(
        IApplicationDbContext db,
        NewsSource entity,
        UpdateNewsSourceCommand request,
        INewsCredentialProtector credentialProtector)
    {
        var portalStrategyKey = request.Portal?.PortalStrategyKey ?? entity.PortalStrategyKey;
        var editionUrl = string.IsNullOrWhiteSpace(request.EditionUrl) ? entity.EditionUrl : request.EditionUrl;
        var baseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? entity.BaseUrl : request.BaseUrl;
        var normalized = NewsSourceUrlRules.ResolveBaseUrl(
            request.SourceType,
            baseUrl,
            editionUrl,
            portalStrategyKey);

        var isManualAssisted = request.ManualLoginRequired
            || request.RequiresMfa
            || request.RequiresOtp
            || request.LoginMethod is PortalLoginMethod.ManualOtpAssisted or PortalLoginMethod.ManualBrowserSession;

        entity.Name = request.Name.Trim();
        entity.BaseUrl = normalized;
        entity.SourceType = request.SourceType;
        entity.AcquisitionMode = request.AcquisitionMode;
        entity.SourceAccessMode = request.SourceAccessMode != NewsSourceAccessMode.Unspecified
            ? request.SourceAccessMode
            : (request.SourceType == NewsSourceType.WebPortalLogin
                ? NewsSourceAccessMode.LicensedSubscriberPortal
                : NewsSourceAccessMode.PublicWeb);
        entity.RequiresLogin = request.RequiresLogin || request.SourceType == NewsSourceType.WebPortalLogin;
        entity.LoginUrl = request.LoginUrl?.Trim();
        entity.EditionUrl = editionUrl?.Trim();
        entity.LogoutUrl = request.LogoutUrl?.Trim();
        entity.PortalUsername = request.PortalUsername?.Trim();
        entity.LoginMethod = request.LoginMethod;
        entity.UsernameSelector = request.UsernameSelector?.Trim();
        entity.PasswordSelector = request.PasswordSelector?.Trim();
        entity.SubmitSelector = request.SubmitSelector?.Trim();
        entity.DownloadSelector = request.DownloadSelector?.Trim();
        entity.LoginSuccessSelector = request.LoginSuccessSelector?.Trim();
        entity.SuccessUrlPattern = request.SuccessUrlPattern?.Trim();
        entity.RequiresCaptcha = request.RequiresCaptcha;
        entity.IsDownloadAllowed = request.IsDownloadAllowed;
        entity.RequiresMfa = request.RequiresMfa;
        entity.RequiresOtp = request.RequiresOtp;
        entity.ManualLoginRequired = request.ManualLoginRequired || isManualAssisted;
        entity.RequiresManualAction = entity.RequiresManualAction || isManualAssisted;
        entity.OtpInstructions = request.OtpInstructions?.Trim();
        entity.AssistedSessionTimeoutMinutes = isManualAssisted
            ? (request.AssistedSessionTimeoutMinutes ?? entity.AssistedSessionTimeoutMinutes ?? 30)
            : request.AssistedSessionTimeoutMinutes;
        entity.Notes = request.Notes?.Trim();
        entity.DefaultLanguage = request.DefaultLanguage;
        entity.Country = request.Country;
        entity.RequiresAuthentication = request.RequiresAuthentication;
        entity.UseHeadlessBrowser = request.UseHeadlessBrowser;
        entity.DownloadFrequencyMinutes = request.DownloadFrequencyMinutes;
        entity.ConnectorKey = request.ConnectorKey;
        entity.SourceCategoryId = request.SourceCategoryId;
        entity.IsEnabled = request.IsEnabled;
        entity.ModifiedAt = DateTimeOffset.UtcNow;

        if (request.PdfDiscovery is { } pdf)
        {
            PdfDiscoveryFieldMapper.Apply(entity, pdf);
        }

        if (request.Portal is { } portal)
        {
            PortalFieldMapper.Apply(entity, portal);
        }

        if (isManualAssisted && entity.Credential is not null)
        {
            entity.Credential.ProtectedCredentialPayload = null;
            entity.Credential.ModifiedAt = DateTimeOffset.UtcNow;
        }

        if (!string.IsNullOrWhiteSpace(request.CronExpression))
        {
            if (entity.DownloadSchedule is null)
            {
                var schedule = new DownloadSchedule
                {
                    Id = Guid.NewGuid(),
                    NewsSourceId = entity.Id,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                entity.DownloadSchedule = schedule;
                db.DownloadSchedules.Add(schedule);
            }

            entity.DownloadSchedule.CronExpression = request.CronExpression.Trim();
            entity.DownloadSchedule.TimeZoneId = string.IsNullOrWhiteSpace(request.ScheduleTimeZoneId) ? "UTC" : request.ScheduleTimeZoneId.Trim();
            entity.DownloadSchedule.IsEnabled = request.ScheduleEnabled;
            entity.DownloadSchedule.ModifiedAt = DateTimeOffset.UtcNow;
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
            if (entity.Credential is null)
            {
                entity.Credential = new SourceCredential
                {
                    Id = Guid.NewGuid(),
                    NewsSourceId = entity.Id,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.SourceCredentials.Add(entity.Credential);
            }

            entity.Credential.ProtectedCredentialPayload = payload;
            entity.Credential.ModifiedAt = DateTimeOffset.UtcNow;
        }
    }
}

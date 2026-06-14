using System.Security.Cryptography;
using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Storage;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Portal;
using MIP.Aws.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace MIP.Aws.Infrastructure.Portal;

/// <summary>Generic licensed portal: CSS form login + optional Download selector click.</summary>
public sealed class GenericWebPortalDownloadStrategy(
    IApplicationDbContext db,
    IFileStorageService fileStorage,
    IOptions<StorageOptions> storageOptions,
    ILogger<GenericWebPortalDownloadStrategy> logger) : IPortalDownloadStrategy
{
    public string StrategyKey => PortalStrategyKeys.Generic;

    public bool CanHandle(NewsSource source) =>
        !PressReaderPortalLogin.IsPressReaderSource(source)
        && !string.Equals(
            PortalFieldMapper.NormalizeStrategyKey(source.PortalStrategyKey),
            PortalStrategyKeys.PressReader,
            StringComparison.Ordinal);

    public Task<PortalLoginStepResult> LoginAsync(PortalAutomationSession session, CancellationToken cancellationToken) =>
        GenericPortalLogin.TryLoginAsync(session.Page, session.Source, session.Username, session.Password, cancellationToken);

    public async Task<PortalEditionDownloadStepResult> DownloadEditionAsync(
        PortalAutomationSession session,
        CancellationToken cancellationToken)
    {
        var source = session.Source;
        var page = session.Page;
        if (string.IsNullOrWhiteSpace(source.EditionUrl))
        {
            return new PortalEditionDownloadStepResult(false, "EditionUrl is not configured.", "EditionUrlMissing");
        }

        await page.GotoAsync(source.EditionUrl!, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle }).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(source.DownloadSelector))
        {
            return new PortalEditionDownloadStepResult(
                false,
                "DownloadSelector is required for generic portal PDF download.",
                "DownloadSelectorMissing");
        }

        var timeoutMs = Math.Clamp(source.DownloadWaitTimeoutSeconds, 30, 600) * 1000;
        try
        {
            var downloadTask = page.WaitForDownloadAsync(new PageWaitForDownloadOptions { Timeout = timeoutMs });
            await page.ClickAsync(source.DownloadSelector!, new PageClickOptions { Timeout = 60_000 }).ConfigureAwait(false);
            var download = await downloadTask.ConfigureAwait(false);
            return await PersistDownloadAsync(session, download, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return new PortalEditionDownloadStepResult(false, "Timed out waiting for portal file download.", "DownloadTimeout");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Generic portal download failed for source {SourceId}", source.Id);
            return new PortalEditionDownloadStepResult(false, ex.Message, "DownloadFailed");
        }
    }

    private async Task<PortalEditionDownloadStepResult> PersistDownloadAsync(
        PortalAutomationSession session,
        IDownload download,
        CancellationToken cancellationToken)
    {
        var source = session.Source;
        var ext = string.IsNullOrWhiteSpace(Path.GetExtension(download.SuggestedFilename)) ? ".pdf" : Path.GetExtension(download.SuggestedFilename);
        var editionDir = PortalStoragePaths.BuildEditionRelativeDirectory(source.Name, storageOptions.Value);
        var editionFileRel = $"{editionDir}/edition{ext}";
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{ext}");
        try
        {
            await download.SaveAsAsync(tempPath).ConfigureAwait(false);
            var bytes = await File.ReadAllBytesAsync(tempPath, cancellationToken).ConfigureAwait(false);
            var minKb = source.MinimumPdfSizeKb > 0 ? source.MinimumPdfSizeKb : 100;
            var validation = PortalPdfFileValidator.Validate(bytes, requirePdfContent: true, minKb);
            if (!validation.Valid)
            {
                return new PortalEditionDownloadStepResult(false, validation.FailureReason!, "NotPdf");
            }

            await fileStorage.WriteAsync(editionFileRel, bytes, cancellationToken).ConfigureAwait(false);
            if (session.DownloadJobId is null)
            {
                return new PortalEditionDownloadStepResult(
                    true,
                    "Edition downloaded (test run).",
                    null,
                    null,
                    editionFileRel,
                    Convert.ToHexString(SHA256.HashData(bytes)),
                    bytes.LongLength);
            }

            var fileId = await PortalStoragePaths.CreateDownloadedFileAsync(
                db,
                session.DownloadJobId!.Value,
                source.EditionUrl!,
                editionFileRel,
                bytes,
                validation.ContentType,
                cancellationToken).ConfigureAwait(false);

            return new PortalEditionDownloadStepResult(
                true,
                "Edition downloaded.",
                null,
                fileId,
                editionFileRel,
                Convert.ToHexString(SHA256.HashData(bytes)),
                bytes.LongLength);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}

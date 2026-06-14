using MIP.Aws.Application.Features.NewsSources;

namespace MIP.Aws.Application.Abstractions.Portal;

/// <summary>
/// Licensed subscriber portal flows using Playwright (login, optional edition download, audit artifacts).
/// </summary>
public interface IWebPortalAutomationService
{
    /// <summary>
    /// Attempts portal login only; persists audit rows and optional failure screenshots/HTML.
    /// </summary>
    Task<NewsPortalLoginTestResultDto> TestLoginAsync(Guid newsSourceId, CancellationToken cancellationToken);

    /// <summary>
    /// Completes a running download job for a WebPortalLogin news source (login, edition navigation, permitted download).
    /// </summary>
    Task RunLicensedPortalDownloadForJobAsync(Guid downloadJobId, CancellationToken cancellationToken);

    /// <summary>
    /// Login + single permitted edition download probe (no Hangfire job); validates PDF before returning.
    /// </summary>
    Task<NewsPortalDownloadTestResultDto> TestDownloadAsync(Guid newsSourceId, CancellationToken cancellationToken);

    /// <summary>
    /// Signs out on the licensed portal (subscriber menu → Sign out) to release concurrent session slots.
    /// </summary>
    Task<NewsPortalLogoutTestResultDto> TestLogoutAsync(Guid newsSourceId, CancellationToken cancellationToken);
}

using MIP.Aws.Domain.Entities;
using Microsoft.Playwright;

namespace MIP.Aws.Infrastructure.Portal;

public interface IPortalDownloadStrategy
{
    string StrategyKey { get; }

    bool CanHandle(NewsSource source);

    Task<PortalLoginStepResult> LoginAsync(PortalAutomationSession session, CancellationToken cancellationToken);

    Task<PortalEditionDownloadStepResult> DownloadEditionAsync(
        PortalAutomationSession session,
        CancellationToken cancellationToken);
}

public sealed class PortalAutomationSession
{
    public required IPage Page { get; init; }
    public required NewsSource Source { get; init; }
    public Guid? DownloadJobId { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
}

public sealed record PortalLoginStepResult(bool Success, string Message, string? FailureCode);

public sealed record PortalEditionDownloadStepResult(
    bool Success,
    string Message,
    string? FailureCode,
    Guid? DownloadedFileId = null,
    string? StoredRelativePath = null,
    string? Sha256 = null,
    long? SizeBytes = null);

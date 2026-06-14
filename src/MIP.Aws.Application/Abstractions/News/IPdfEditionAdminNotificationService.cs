using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Application.Abstractions.News;

/// <summary>Notifies operators when scheduled PDF downloads need manual follow-up.</summary>
public interface IPdfEditionAdminNotificationService
{
    Task SendManualActionRequiredAsync(
        IReadOnlyList<PdfEditionJobResult> results,
        DateOnly editionDate,
        CancellationToken cancellationToken);
}

public sealed record PdfEditionJobResult(
    Guid NewsSourceId,
    string SourceName,
    string? DiscoveryPageUrl,
    string? ConnectorKey,
    PdfEditionStatus Status,
    string? FailureReason,
    string? LastCandidateUrl);

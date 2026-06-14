using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Application.Abstractions.News;

/// <summary>
/// Discovers publicly available PDF edition links on publisher websites (no paywall/login bypass).
/// </summary>
public interface IPdfEditionDiscoveryService
{
    /// <param name="allowPlaywright">When false, uses HTTP-only discovery/validation (avoids launching headless Chromium on the server).</param>
    Task<PdfEditionDiscoveryResult> DiscoverAsync(NewsSource source, bool allowPlaywright, CancellationToken cancellationToken);
}

public sealed record PdfEditionCandidate(
    Uri Url,
    double Confidence,
    PdfDiscoveryMethod Method,
    string? Label,
    bool IsTodayEdition);

public sealed record PdfEditionDiscoveryResult(
    IReadOnlyList<PdfEditionCandidate> Candidates,
    PdfEditionCandidate? BestCandidate,
    string? PageUrl,
    string? DiscoveryFailureReason = null);

public sealed record PdfEditionValidationResult(
    bool IsValid,
    string? ContentType,
    long? SizeBytes,
    string? FailureReason,
    byte[]? ValidatedPdfBytes = null);

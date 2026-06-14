using MIP.Aws.Application.Features.SourceRecovery;

namespace MIP.Aws.Application.Abstractions.Intelligence;

public interface IAiRecoveryProvider
{
    Task<SourceRecoveryAnalysisDto> AnalyzeAsync(SourceRecoveryAnalysisRequest request, CancellationToken cancellationToken = default);
}

public sealed record SourceRecoveryAnalysisRequest(
    Guid SourceId,
    string SourceName,
    string FailureType,
    string FailureMessage,
    string? HtmlSnapshot,
    string? ScreenshotBase64);

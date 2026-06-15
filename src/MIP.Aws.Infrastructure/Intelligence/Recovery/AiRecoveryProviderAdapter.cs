using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Features.SourceRecovery;

namespace MIP.Aws.Infrastructure.Intelligence.Recovery;

public sealed class AiRecoveryProviderAdapter(IAISourceRecoveryService sourceRecovery) : IAiRecoveryProvider
{
    public async Task<SourceRecoveryAnalysisDto> AnalyzeAsync(
        SourceRecoveryAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        var options = await sourceRecovery.GenerateRecoveryOptionsAsync(
            new SourceRecoveryAnalysisContext(
                request.SourceId,
                request.SourceName,
                Guid.Empty,
                request.FailureType,
                string.Empty,
                request.FailureMessage,
                null,
                null,
                null,
                0,
                DateTimeOffset.UtcNow,
                "{}",
                null,
                null,
                null,
                null,
                request.HtmlSnapshot,
                request.ScreenshotBase64,
                null,
                null,
                [],
                [],
                []),
            cancellationToken).ConfigureAwait(false);

        return new SourceRecoveryAnalysisDto(
            Guid.NewGuid(),
            request.SourceId,
            request.SourceName,
            null,
            request.FailureType,
            request.FailureMessage,
            null,
            DateTimeOffset.UtcNow,
            0,
            null,
            null,
            options,
            sourceRecovery.RecommendBestOptionIndex(options),
            [],
            [],
            sourceRecovery.IsEnabled);
    }
}

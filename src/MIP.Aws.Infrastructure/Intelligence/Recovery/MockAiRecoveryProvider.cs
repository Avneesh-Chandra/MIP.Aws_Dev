using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Features.SourceRecovery;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Infrastructure.Intelligence.Recovery;

public sealed class MockAiRecoveryProvider : IAiRecoveryProvider
{
    public Task<SourceRecoveryAnalysisDto> AnalyzeAsync(SourceRecoveryAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        var patch = new SourceRecoveryConfigurationPatchDto(
            UsernameSelector: null,
            PasswordSelector: null,
            SubmitSelector: null,
            DownloadSelector: null,
            LoginIconSelector: null,
            NewspaperCanvasSelector: null,
            ContextMenuSelector: null,
            DownloadMenuItemSelector: null,
            LoginSuccessSelector: null,
            SuccessUrlPattern: null,
            PdfDownloadSelector: null,
            PdfLinkSelector: null,
            BaseUrl: null,
            EditionUrl: null,
            PdfDiscoveryPageUrl: null,
            DownloadWaitTimeoutSeconds: null,
            UseHeadlessBrowser: true);
        var option = new SourceRecoveryOptionDto(
            OptionIndex: 0,
            Title: "Retry with Playwright headless",
            Description: "Use headless browser and wait for network idle before PDF capture.",
            ExpectedFix: "Improves dynamic page load reliability.",
            ConfidenceScore: 85,
            PredictedSuccessPercent: 85,
            RiskLevel: SourceRecoveryRiskLevel.Low,
            AffectedFields: ["UseHeadlessBrowser"],
            AffectedWorkflowSteps: ["portal_login", "pdf_download"],
            Patch: patch,
            SelectorCandidates: []);

        var analysis = new SourceRecoveryAnalysisDto(
            AttemptId: Guid.NewGuid(),
            SourceId: request.SourceId,
            SourceName: request.SourceName,
            DownloadJobId: null,
            FailureType: request.FailureType,
            FailureMessage: request.FailureMessage,
            SourceUrl: null,
            AttemptedAt: DateTimeOffset.UtcNow,
            RetryCount: 0,
            ScreenshotUrl: null,
            HtmlSnapshotUrl: null,
            Options: [option],
            RecommendedOptionIndex: 0,
            ScreenshotFindings: [],
            HtmlFindings: [],
            AiEnabled: true);

        return Task.FromResult(analysis);
    }
}

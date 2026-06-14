using MIP.Aws.Application.Features.SourceRecovery;

namespace MIP.Aws.Application.Abstractions.Intelligence;

public interface IAISourceRecoveryService
{
    bool IsEnabled { get; }

    Task<SourceRecoveryAnalysisDto> AnalyzeFailureAsync(
        Guid newsSourceId,
        Guid downloadJobId,
        Guid actorUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SourceRecoveryOptionDto>> GenerateRecoveryOptionsAsync(
        SourceRecoveryAnalysisContext context,
        CancellationToken cancellationToken);

    int PredictSuccessProbability(SourceRecoveryOptionDto option);

    int RecommendBestOptionIndex(IReadOnlyList<SourceRecoveryOptionDto> options);

    SourceRecoveryConfigurationPatchDto CreateConfigurationPatch(SourceRecoveryOptionDto option);
}

/// <summary>Inputs collected for AI recovery analysis.</summary>
public sealed record SourceRecoveryAnalysisContext(
    Guid SourceId,
    string SourceName,
    Guid DownloadJobId,
    string FailureType,
    string FailureCode,
    string FailureMessage,
    string? SourceUrl,
    string? EditionUrl,
    string? LoginUrl,
    int RetryCount,
    DateTimeOffset? AttemptedAt,
    string CurrentConfigurationJson,
    string? LastSuccessfulConfigurationJson,
    string? PlaywrightLogExcerpt,
    string? BrowserConsoleLog,
    string? NetworkLogExcerpt,
    string? HtmlSnapshot,
    string? ScreenshotReference,
    string? HtmlSnapshotReference,
    string? LastSuccessfulPdfMetadataJson,
    IReadOnlyList<string> OperatorNotes,
    IReadOnlyList<string> AdminNotes,
    IReadOnlyList<SourceRecoveryKnowledgeHint> KnowledgeHints);

public sealed record SourceRecoveryKnowledgeHint(
    string FailureType,
    string FieldName,
    string? OldSelector,
    string NewSelector,
    int SuccessCount);

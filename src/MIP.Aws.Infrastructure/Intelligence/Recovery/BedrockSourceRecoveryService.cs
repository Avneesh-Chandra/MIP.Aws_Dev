using System.Text;
using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.SourceRecovery;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Intelligence.Recovery;

public sealed class BedrockSourceRecoveryService(
    IAiTextGenerationService textGeneration,
    IOptions<AiOptions> aiOptions,
    IOptions<AiSourceRecoveryOptions> recoveryOptions,
    ILogger<BedrockSourceRecoveryService> logger) : IAISourceRecoveryService
{
    private readonly AiOptions _ai = aiOptions.Value;
    private readonly AiSourceRecoveryOptions _recovery = recoveryOptions.Value;

    public bool IsEnabled =>
        _ai.Enabled
        && _recovery.Enabled
        && textGeneration.IsEnabled;

    public Task<SourceRecoveryAnalysisDto> AnalyzeFailureAsync(
        Guid newsSourceId,
        Guid downloadJobId,
        Guid actorUserId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Use SourceRecoveryService facade for full analysis persistence.");

    public async Task<IReadOnlyList<SourceRecoveryOptionDto>> GenerateRecoveryOptionsAsync(
        SourceRecoveryAnalysisContext context,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return SourceRecoveryHeuristicBuilder.BuildOptions(context);
        }

        try
        {
            var html = Truncate(context.HtmlSnapshot, _recovery.MaxHtmlChars);
            var userPrompt = BuildUserPrompt(context, html);
            var result = await textGeneration.GenerateAsync(
                new AiTextGenerationRequest(SourceRecoveryAiPrompts.RecoverySystemPrompt, userPrompt, RequireJson: true),
                cancellationToken).ConfigureAwait(false);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
            {
                logger.LogWarning(
                    "AI source recovery failed for source {SourceId}: {Error}; using heuristics.",
                    context.SourceId,
                    result.Error ?? "unknown");
                return SourceRecoveryHeuristicBuilder.BuildOptions(context);
            }

            if (!AiRecoveryResponseParser.TryParse(result.Text, out var parsed, out var parseError))
            {
                logger.LogWarning(
                    "AI source recovery returned invalid JSON for source {SourceId}: {Error}",
                    context.SourceId,
                    parseError);
                return SourceRecoveryHeuristicBuilder.BuildOptions(context);
            }

            if (!AiRecoveryResponseValidator.TryValidateRawJson(result.Text, out var validationError))
            {
                logger.LogWarning(
                    "AI source recovery returned unsafe JSON for source {SourceId}: {Error}",
                    context.SourceId,
                    validationError);
                return SourceRecoveryHeuristicBuilder.BuildOptions(context);
            }

            var options = AiRecoveryResponseValidator.SanitizeOptions(parsed.Options);
            return options.Count > 0
                ? SourceRecoveryHeuristicBuilder.MergePublisherHeuristics(context, options)
                : SourceRecoveryHeuristicBuilder.BuildOptions(context);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI source recovery failed for source {SourceId}; using heuristics.", context.SourceId);
            return SourceRecoveryHeuristicBuilder.BuildOptions(context);
        }
    }

    public int PredictSuccessProbability(SourceRecoveryOptionDto option) => option.PredictedSuccessPercent;

    public int RecommendBestOptionIndex(IReadOnlyList<SourceRecoveryOptionDto> options)
    {
        if (options.Count == 0)
        {
            return -1;
        }

        return options
            .OrderByDescending(o => o.ConfidenceScore)
            .ThenByDescending(o => o.PredictedSuccessPercent)
            .First()
            .OptionIndex;
    }

    public SourceRecoveryConfigurationPatchDto CreateConfigurationPatch(SourceRecoveryOptionDto option) => option.Patch;

    private static string BuildUserPrompt(SourceRecoveryAnalysisContext context, string? html)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Source: {context.SourceName}");
        sb.AppendLine($"FailureType: {context.FailureType}");
        sb.AppendLine($"FailureCode: {context.FailureCode}");
        sb.AppendLine($"FailureMessage: {context.FailureMessage}");
        sb.AppendLine($"SourceUrl: {context.SourceUrl}");
        sb.AppendLine($"RetryCount: {context.RetryCount}");
        sb.AppendLine($"CurrentConfiguration: {context.CurrentConfigurationJson}");
        if (!string.IsNullOrWhiteSpace(context.LastSuccessfulConfigurationJson))
        {
            sb.AppendLine($"LastSuccessfulConfiguration: {context.LastSuccessfulConfigurationJson}");
        }

        if (!string.IsNullOrWhiteSpace(context.PlaywrightLogExcerpt))
        {
            sb.AppendLine("PlaywrightLog:").AppendLine(context.PlaywrightLogExcerpt);
        }

        if (context.KnowledgeHints.Count > 0)
        {
            sb.AppendLine("PriorSuccessfulFixes:");
            foreach (var hint in context.KnowledgeHints)
            {
                sb.AppendLine($"- {hint.FailureType}/{hint.FieldName}: {hint.OldSelector} -> {hint.NewSelector} (success={hint.SuccessCount})");
            }
        }

        if (!string.IsNullOrWhiteSpace(html))
        {
            sb.AppendLine("HtmlSnapshot:").AppendLine(html);
        }

        if (!string.IsNullOrWhiteSpace(context.ScreenshotReference))
        {
            sb.AppendLine($"ScreenshotReference: {context.ScreenshotReference}");
        }

        return sb.ToString();
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}

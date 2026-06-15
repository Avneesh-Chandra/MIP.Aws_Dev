using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Abstractions.Operator;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.Operator;
using MIP.Aws.Application.Features.SourceRecovery;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Operator;

public interface IDownloadMonitorStatusSummaryService
{
    Task<string> BuildSummaryAsync(DownloadMonitorDto monitor, CancellationToken cancellationToken);
}

public sealed class DownloadMonitorStatusSummaryService(
    IAiTextGenerationService textGeneration,
    IOptions<AiOptions> aiOptions) : IDownloadMonitorStatusSummaryService
{
    public async Task<string> BuildSummaryAsync(DownloadMonitorDto monitor, CancellationToken cancellationToken)
    {
        if (!aiOptions.Value.Enabled || !textGeneration.IsEnabled)
        {
            return BuildDeterministicSummary(monitor);
        }

        var userPrompt = $"""
            MonitorDate: {monitor.MonitorDate:yyyy-MM-dd}
            TotalSources: {monitor.Summary.TotalSources}
            SuccessfulToday: {monitor.Summary.SuccessfulToday}
            FailedToday: {monitor.Summary.FailedToday}
            ManualIntervention: {monitor.Summary.PendingManualIntervention}
            PdfsDownloadedToday: {monitor.Summary.PdfsDownloadedToday}
            AttentionSources: {string.Join("; ", monitor.Summary.SourcesRequiringAttention.Select(s => $"{s.SourceName}: {s.Issue}"))}
            """;

        var result = await textGeneration.GenerateAsync(
            new AiTextGenerationRequest(SourceRecoveryAiPrompts.StatusEmailSummarySystemPrompt, userPrompt),
            cancellationToken).ConfigureAwait(false);

        return result.Success && !string.IsNullOrWhiteSpace(result.Text)
            ? result.Text.Trim()
            : BuildDeterministicSummary(monitor);
    }

    public static string BuildDeterministicSummary(DownloadMonitorDto monitor)
    {
        var s = monitor.Summary;
        return $"Daily download monitor for {monitor.MonitorDate:yyyy-MM-dd}: {s.SuccessfulToday} of {s.TotalSources} sources succeeded, {s.FailedToday} failed, {s.PendingManualIntervention} require manual intervention, and {s.PdfsDownloadedToday} PDFs were stored.";
    }
}

using MIP.Aws.Application.Features.Operator;

namespace MIP.Aws.Tests;

public sealed class ManualInterventionSuggestionsTests
{
    [Fact]
    public void RequiresManualIntervention_includes_failed_after_auto_ai_recovery()
    {
        var result = ManualInterventionSuggestions.RequiresManualIntervention(
            DownloadMonitorStatusLabels.FailedAfterAutoAiRecovery,
            failureCode: null,
            requiresManualAction: false,
            complianceBlocked: false);

        Assert.True(result);
    }

    [Fact]
    public void GetSuggestion_returns_auto_recovery_guidance_when_exhausted()
    {
        var suggestion = ManualInterventionSuggestions.GetSuggestion(
            DownloadMonitorStatusLabels.FailedAfterAutoAiRecovery,
            failureCode: null,
            failureMessage: null,
            requiresManualAction: false,
            complianceBlocked: false);

        Assert.Contains("Automatic AI recovery", suggestion, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("inform Admin", suggestion, StringComparison.OrdinalIgnoreCase);
    }
}

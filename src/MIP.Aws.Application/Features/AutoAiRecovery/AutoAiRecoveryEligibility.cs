using MIP.Aws.Application.Configuration;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Domain.Security;

namespace MIP.Aws.Application.Features.AutoAiRecovery;

public static class AutoAiRecoveryEligibility
{
    public static bool IsSourceTypeAllowed(NewsSource source, AutoAiDownloadRecoveryOptions options) =>
        options.OnlyForSourceTypes.Any(t =>
            string.Equals(t, source.SourceType.ToString(), StringComparison.OrdinalIgnoreCase));

    public static bool IsSourceEnabled(NewsSource source, bool globalEnabled) =>
        globalEnabled && (source.AutoAiRecoveryEnabled ?? true);

    public static bool IsJobEligibleForAutoRecovery(DownloadJob job) =>
        job.Status == DownloadJobStatus.Failed
        && job.Trigger is not DownloadJobTrigger.Recovery
        && job.Trigger is not DownloadJobTrigger.AutoAiRecovery
        && !IsRecoveryCorrelation(job.CorrelationId);

    public static bool ShouldRunForTrigger(DownloadJobTrigger trigger, AutoAiDownloadRecoveryOptions options) =>
        trigger switch
        {
            DownloadJobTrigger.Scheduled => options.RunAfterScheduledFailure,
            DownloadJobTrigger.Manual => options.RunAfterManualFailure,
            DownloadJobTrigger.Retry => options.RunAfterManualFailure,
            _ => false
        };

    public static bool RequiresManualIntervention(NewsSource source, string? failureCode, string? failureType)
    {
        if (!source.IsDownloadAllowed)
        {
            return true;
        }

        if (source.RequiresManualAction || source.RequiresCaptcha || source.RequiresMfa || source.RequiresOtp || source.ManualLoginRequired)
        {
            return true;
        }

        if (failureType is SourceRecoveryFailureTypes.CaptchaDetected or SourceRecoveryFailureTypes.MfaDetected)
        {
            return true;
        }

        if (string.Equals(failureCode, "RequiresCaptcha", StringComparison.OrdinalIgnoreCase)
            || string.Equals(failureCode, "MfaOnLoginPage", StringComparison.OrdinalIgnoreCase)
            || string.Equals(failureCode, "CredentialsNeedReEntry", StringComparison.OrdinalIgnoreCase)
            || string.Equals(failureCode, "InvalidCredentials", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsRecoveryCorrelation(string? correlationId) =>
        !string.IsNullOrWhiteSpace(correlationId)
        && correlationId.StartsWith("recovery:", StringComparison.OrdinalIgnoreCase);
}

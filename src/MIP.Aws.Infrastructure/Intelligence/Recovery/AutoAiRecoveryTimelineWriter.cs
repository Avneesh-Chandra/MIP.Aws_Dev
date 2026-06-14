using MIP.Aws.Application.Features.AutoAiRecovery;
using MIP.Aws.Domain.Entities;

namespace MIP.Aws.Infrastructure.Intelligence.Recovery;

internal static class AutoAiRecoveryTimelineWriter
{
    public static void AddStep(AutoAiRecoveryRun run, string step, string detail, bool succeeded = true)
    {
        var steps = AutoAiRecoveryTimelineJson.Deserialize(run.TimelineJson).ToList();
        steps.Add(new AutoAiRecoveryTimelineStepDto(
            steps.Count + 1,
            step,
            DateTimeOffset.UtcNow,
            detail,
            succeeded));
        run.TimelineJson = AutoAiRecoveryTimelineJson.Serialize(steps);
    }
}

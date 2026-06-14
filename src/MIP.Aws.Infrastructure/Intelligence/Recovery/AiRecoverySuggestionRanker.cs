using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.AutoAiRecovery;
using MIP.Aws.Application.Features.SourceRecovery;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Infrastructure.Intelligence.Recovery;

public sealed class AiRecoverySuggestionRanker : IAiRecoverySuggestionRanker
{
    public IReadOnlyList<SourceRecoveryOptionDto> RankForAutoRecovery(
        IReadOnlyList<SourceRecoveryOptionDto> options,
        AutoAiDownloadRecoveryOptions settings)
    {
        return options
            .Where(o => AutoAiRecoveryPatchValidator.IsOptionSafeForAutoApply(o, settings, out _))
            .OrderByDescending(Score)
            .ThenBy(o => o.RiskLevel)
            .Take(Math.Max(1, settings.MaxSuggestionsToTry))
            .ToList();
    }

    internal static double Score(SourceRecoveryOptionDto option)
    {
        var riskWeight = option.RiskLevel switch
        {
            SourceRecoveryRiskLevel.Low => 1.0,
            SourceRecoveryRiskLevel.Medium => 0.6,
            _ => 0.0
        };

        return option.ConfidenceScore * 0.4
               + option.PredictedSuccessPercent * 0.4
               + riskWeight * 100 * 0.2;
    }
}

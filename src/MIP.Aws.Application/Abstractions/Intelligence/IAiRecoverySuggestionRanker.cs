using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.SourceRecovery;

namespace MIP.Aws.Application.Abstractions.Intelligence;

public interface IAiRecoverySuggestionRanker
{
    IReadOnlyList<SourceRecoveryOptionDto> RankForAutoRecovery(
        IReadOnlyList<SourceRecoveryOptionDto> options,
        AutoAiDownloadRecoveryOptions settings);
}

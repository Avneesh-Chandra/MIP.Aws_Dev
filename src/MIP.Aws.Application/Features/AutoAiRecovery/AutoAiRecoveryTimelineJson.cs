using System.Text.Json;

namespace MIP.Aws.Application.Features.AutoAiRecovery;

public static class AutoAiRecoveryTimelineJson
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static IReadOnlyList<AutoAiRecoveryTimelineStepDto> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<AutoAiRecoveryTimelineStepDto>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static string Serialize(IReadOnlyList<AutoAiRecoveryTimelineStepDto> steps) =>
        JsonSerializer.Serialize(steps, JsonOptions);
}

using System.Text.Json;

namespace MIP.Aws.Infrastructure.Intelligence.Bedrock;

public sealed class ClaudeBedrockPromptAdapter : IBedrockPromptAdapter
{
    public bool CanHandle(string modelId) =>
        modelId.Contains("anthropic.claude", StringComparison.OrdinalIgnoreCase);

    public string BuildRequestBody(
        string systemPrompt,
        string userPrompt,
        int maxTokens,
        double temperature,
        double topP)
    {
        var payload = new
        {
            anthropic_version = "bedrock-2023-05-31",
            max_tokens = maxTokens,
            temperature,
            top_p = topP,
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = userPrompt }
            }
        };

        return JsonSerializer.Serialize(payload);
    }
}

using System.Text.Json;

namespace MIP.Aws.Infrastructure.Intelligence.Bedrock;

public sealed class NovaBedrockPromptAdapter : IBedrockPromptAdapter
{
    public bool CanHandle(string modelId) =>
        modelId.Contains("amazon.nova", StringComparison.OrdinalIgnoreCase);

    public string BuildRequestBody(
        string systemPrompt,
        string userPrompt,
        int maxTokens,
        double temperature,
        double topP)
    {
        var combined = string.IsNullOrWhiteSpace(systemPrompt)
            ? userPrompt
            : $"{systemPrompt}\n\n{userPrompt}";

        var payload = new
        {
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new[] { new { text = combined } }
                }
            },
            inferenceConfig = new
            {
                maxTokens,
                temperature,
                topP
            }
        };

        return JsonSerializer.Serialize(payload);
    }
}

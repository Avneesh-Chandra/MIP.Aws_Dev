namespace MIP.Aws.Infrastructure.Intelligence.Bedrock;

public interface IBedrockPromptAdapter
{
    bool CanHandle(string modelId);

    string BuildRequestBody(
        string systemPrompt,
        string userPrompt,
        int maxTokens,
        double temperature,
        double topP);
}

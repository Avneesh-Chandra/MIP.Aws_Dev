namespace MIP.Aws.Infrastructure.Intelligence.Bedrock;

public sealed class BedrockPromptAdapterFactory(IEnumerable<IBedrockPromptAdapter> adapters)
{
    public IBedrockPromptAdapter Resolve(string modelId)
    {
        var match = adapters.FirstOrDefault(a => a.CanHandle(modelId));
        if (match is null)
        {
            throw new InvalidOperationException(
                $"No Bedrock prompt adapter registered for model '{modelId}'. Supported families: Anthropic Claude, Amazon Nova.");
        }

        return match;
    }
}

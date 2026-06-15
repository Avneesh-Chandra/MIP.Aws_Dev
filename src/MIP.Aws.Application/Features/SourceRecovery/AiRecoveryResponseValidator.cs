using System.Text.Json;

namespace MIP.Aws.Application.Features.SourceRecovery;

/// <summary>Rejects unsafe AI recovery patches and invalid JSON payloads.</summary>
public static class AiRecoveryResponseValidator
{
    private static readonly HashSet<string> ForbiddenPatchFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "usernameSelector",
        "passwordSelector",
        "credentials",
        "oauth",
        "isDownloadAllowed",
        "requiresLogin",
        "requiresMfa",
        "requiresCaptcha",
        "compliance",
        "license",
        "licensing",
        "secret",
        "apiKey",
        "storagePath"
    };

    public static bool TryValidateRawJson(string raw, out string? error)
    {
        error = null;
        if (!AiRecoveryResponseParser.TryParse(raw, out var parsed, out var parseError))
        {
            error = parseError;
            return false;
        }

        if (parsed.Options.Count == 0)
        {
            error = "No recovery suggestions returned.";
            return false;
        }

        foreach (var option in parsed.Options)
        {
            if (!IsPatchSafe(option.Patch, out var rejected))
            {
                error = $"Unsafe patch in '{option.Title}': {string.Join(", ", rejected)}";
                return false;
            }
        }

        if (ContainsBlockedPatchViolations(raw, out var blockedError))
        {
            error = blockedError;
            return false;
        }

        return true;
    }

    public static IReadOnlyList<SourceRecoveryOptionDto> SanitizeOptions(IReadOnlyList<SourceRecoveryOptionDto> options)
    {
        var sanitized = new List<SourceRecoveryOptionDto>();
        foreach (var option in options)
        {
            if (!IsPatchSafe(option.Patch, out _))
            {
                continue;
            }

            sanitized.Add(option);
        }

        return SourceRecoveryOptionSanitizer.Sanitize(sanitized);
    }

    public static bool IsPatchSafe(SourceRecoveryConfigurationPatchDto patch, out IReadOnlyList<string> rejectedFields)
    {
        var rejected = new List<string>();
        if (patch.UsernameSelector is not null)
        {
            rejected.Add(nameof(patch.UsernameSelector));
        }

        if (patch.PasswordSelector is not null)
        {
            rejected.Add(nameof(patch.PasswordSelector));
        }

        rejectedFields = rejected;
        return rejected.Count == 0;
    }

    private static bool ContainsBlockedPatchViolations(string raw, out string? error)
    {
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("suggestions", out var suggestions)
                || suggestions.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var item in suggestions.EnumerateArray())
            {
                if (!TryGetProperty(item, "blockedPatch", out var blocked) || blocked.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var prop in blocked.EnumerateObject())
                {
                    if (prop.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                    {
                        continue;
                    }

                    if (prop.Value.ValueKind == JsonValueKind.Object && prop.Value.EnumerateObject().Any())
                    {
                        error = $"blockedPatch must remain empty; found nested object on '{prop.Name}'.";
                        return true;
                    }

                    if (prop.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(prop.Value.GetString()))
                    {
                        error = $"blockedPatch must remain empty; found value on '{prop.Name}'.";
                        return true;
                    }

                    if (ForbiddenPatchFields.Contains(prop.Name))
                    {
                        error = $"blockedPatch contains forbidden field '{prop.Name}'.";
                        return true;
                    }
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool TryGetProperty(JsonElement el, string name, out JsonElement value)
    {
        value = default;
        foreach (var prop in el.EnumerateObject())
        {
            if (!prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = prop.Value;
            return true;
        }

        return false;
    }
}

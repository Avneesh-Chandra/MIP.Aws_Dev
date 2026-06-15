using System.Text.Json;
using MIP.Aws.Application.Features.SourceRecovery;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Application.Features.SourceRecovery;

/// <summary>Parses strict JSON recovery responses (legacy <c>options</c> and Bedrock <c>suggestions</c> formats).</summary>
public static class AiRecoveryResponseParser
{
    public static bool TryParse(string raw, out AiRecoveryParseResult result, out string? error)
    {
        result = default!;
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "Empty AI response.";
            return false;
        }

        var json = StripMarkdownFences(raw.Trim());

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "Root JSON must be an object.";
                return false;
            }

            var summary = GetString(root, "summary");
            var screenshotFindings = SourceRecoveryJsonParser.ParseStringArray(root, "screenshotFindings");
            var htmlFindings = SourceRecoveryJsonParser.ParseStringArray(root, "htmlFindings");

            IReadOnlyList<SourceRecoveryOptionDto> options;
            if (TryGetProperty(root, "suggestions", out var suggestionsEl) && suggestionsEl.ValueKind == JsonValueKind.Array)
            {
                options = ParseSuggestions(suggestionsEl);
            }
            else if (TryGetProperty(root, "options", out _))
            {
                options = SourceRecoveryJsonParser.ParseOptions(json);
            }
            else
            {
                error = "Response must contain 'suggestions' or 'options' array.";
                return false;
            }

            result = new AiRecoveryParseResult(summary, options, screenshotFindings, htmlFindings);
            return true;
        }
        catch (JsonException ex)
        {
            error = $"Invalid JSON: {ex.Message}";
            return false;
        }
    }

    private static IReadOnlyList<SourceRecoveryOptionDto> ParseSuggestions(JsonElement suggestionsEl)
    {
        var list = new List<SourceRecoveryOptionDto>();
        var index = 0;
        foreach (var item in suggestionsEl.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            TryGetProperty(item, "allowedPatch", out var patchEl);
            if (patchEl.ValueKind == JsonValueKind.Undefined)
            {
                TryGetProperty(item, "patch", out patchEl);
            }

            var patch = ParsePatch(patchEl);
            var confidence = NormalizePercent(GetDouble(item, "confidence") ?? GetInt(item, "confidenceScore"));
            var predicted = NormalizePercent(GetDouble(item, "predictedSuccess") ?? GetInt(item, "predictedSuccessPercent"));
            var reason = GetString(item, "reason") ?? GetString(item, "expectedFix") ?? string.Empty;

            list.Add(new SourceRecoveryOptionDto(
                GetInt(item, "optionIndex") ?? index,
                GetString(item, "title") ?? $"Recovery option {index + 1}",
                GetString(item, "description") ?? string.Empty,
                reason,
                confidence,
                predicted,
                ParseRisk(GetString(item, "risk") ?? GetString(item, "riskLevel")),
                SourceRecoveryJsonParser.ParseStringArray(item, "affectedFields"),
                SourceRecoveryJsonParser.ParseStringArray(item, "affectedWorkflowSteps"),
                patch,
                []));
            index++;
        }

        return list;
    }

    private static SourceRecoveryConfigurationPatchDto ParsePatch(JsonElement patchEl)
    {
        if (patchEl.ValueKind != JsonValueKind.Object)
        {
            return new SourceRecoveryConfigurationPatchDto(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
        }

        return new SourceRecoveryConfigurationPatchDto(
            GetString(patchEl, "usernameSelector"),
            GetString(patchEl, "passwordSelector"),
            GetString(patchEl, "submitSelector"),
            GetString(patchEl, "downloadSelector"),
            GetString(patchEl, "loginIconSelector"),
            GetString(patchEl, "newspaperCanvasSelector"),
            GetString(patchEl, "contextMenuSelector"),
            GetString(patchEl, "downloadMenuItemSelector"),
            GetString(patchEl, "loginSuccessSelector"),
            GetString(patchEl, "successUrlPattern"),
            GetString(patchEl, "pdfDownloadSelector"),
            GetString(patchEl, "pdfLinkSelector"),
            GetString(patchEl, "baseUrl"),
            GetString(patchEl, "editionUrl"),
            GetString(patchEl, "pdfDiscoveryPageUrl"),
            GetInt(patchEl, "downloadWaitTimeoutSeconds"),
            GetBool(patchEl, "useHeadlessBrowser"));
    }

    private static int NormalizePercent(double? value)
    {
        if (value is null)
        {
            return 60;
        }

        return value.Value switch
        {
            <= 1 => (int)Math.Round(value.Value * 100),
            _ => (int)Math.Round(value.Value)
        };
    }

    private static string StripMarkdownFences(string raw)
    {
        if (!raw.StartsWith("```", StringComparison.Ordinal))
        {
            return raw;
        }

        var start = raw.IndexOf('\n');
        if (start < 0)
        {
            return raw;
        }

        var end = raw.LastIndexOf("```", StringComparison.Ordinal);
        return end <= start ? raw[(start + 1)..] : raw[(start + 1)..end].Trim();
    }

    private static bool TryGetProperty(JsonElement el, string name, out JsonElement value)
    {
        value = default;
        if (el.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

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

    private static string? GetString(JsonElement el, string name)
    {
        if (!TryGetProperty(el, name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static int? GetInt(JsonElement el, string name)
    {
        if (!TryGetProperty(el, name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var i) => i,
            _ => null
        };
    }

    private static double? GetDouble(JsonElement el, string name)
    {
        if (!TryGetProperty(el, name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDouble(out var d) => d,
            _ => null
        };
    }

    private static bool? GetBool(JsonElement el, string name)
    {
        if (!TryGetProperty(el, name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static SourceRecoveryRiskLevel ParseRisk(string? value) => value?.ToLowerInvariant() switch
    {
        "low" => SourceRecoveryRiskLevel.Low,
        "high" => SourceRecoveryRiskLevel.High,
        _ => SourceRecoveryRiskLevel.Medium
    };
}

public sealed record AiRecoveryParseResult(
    string? Summary,
    IReadOnlyList<SourceRecoveryOptionDto> Options,
    IReadOnlyList<string> ScreenshotFindings,
    IReadOnlyList<string> HtmlFindings);

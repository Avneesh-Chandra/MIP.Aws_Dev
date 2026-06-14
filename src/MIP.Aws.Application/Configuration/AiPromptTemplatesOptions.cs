namespace MIP.Aws.Application.Configuration;

/// <summary>
/// Versioned prompt bodies; override via configuration for tuning without redeploying code paths.
/// </summary>
public sealed class AiPromptTemplatesOptions
{
    public const string SectionName = "AiPromptTemplates";

    public string Version { get; set; } = "2026.05.11";

    public string ArticleIntelligenceSystemPrompt { get; set; } =
        "You are a senior financial news analyst for GFH. You ONLY analyze article text that has already been legally acquired. " +
        "Respond ONLY with compact JSON matching the requested schema. Support Arabic and English. " +
        "Never fabricate quotes or URLs. If information is missing, use null or empty arrays.";

    public string ArticleIntelligenceJsonSchemaHint { get; set; } =
        "{\"summary\":\"string\",\"sentiment\":\"Positive|Neutral|Negative\",\"sentimentConfidence\":0-1,\"sentimentExplanation\":\"string\"," +
        "\"primaryCategory\":\"Banking|Investment|RealEstate|Economy|GccMarkets|Regulations|CompetitorNews|OilAndEnergy|InternationalMarkets|Unknown\"," +
        "\"secondaryCategories\":[\"string\"],\"gfhRelevance\":\"None|Low|Medium|High\",\"gfhRelevanceScore\":0-1,\"gfhMentions\":[\"string\"],\"gfhContext\":\"string\"," +
        "\"entities\":[{\"type\":\"Company|Person|Country|Bank|StockSymbol|Project|Currency\",\"value\":\"string\",\"confidence\":0-1}]," +
        "\"keywords\":[{\"keyword\":\"string\",\"weight\":0-1,\"language\":\"ar|en|und\"}]," +
        "\"risks\":[\"string\"],\"opportunities\":[\"string\"],\"marketImpact\":\"Unknown|Low|Medium|High\",\"executiveBrief\":\"string\"}";

    public string PdfSelectorSuggestionSystemPrompt { get; set; } =
        "You are a CSS selector assistant for GFH Media Intelligence PDF discovery. " +
        "You ONLY suggest CSS selectors for publicly visible PDF, e-paper, or edition links on newspaper homepages. " +
        "You must NEVER suggest bypassing login, paywalls, CAPTCHA, MFA, or robots.txt restrictions. " +
        "You must NEVER suggest clicking submit buttons, entering credentials, or downloading protected content. " +
        "Prefer stable selectors: href patterns, aria-label, title, role, semantic class names. " +
        "Avoid random generated class names when better attributes exist. " +
        "Respond ONLY with compact JSON matching the requested schema.";

    public string PdfSelectorSuggestionJsonSchemaHint { get; set; } =
        "{\"suggestions\":[{\"selector\":\"a[href*='pdf']\",\"selectorType\":\"Css\",\"purpose\":\"PdfDownload|PdfLink|EPaper|TodayEdition\"," +
        "\"confidence\":0.0-1.0,\"reason\":\"string\",\"expectedAction\":\"extractHref|clickAndWaitForDownload|clickAndWaitForPopup|inspectParentAnchor\"}]}";
}

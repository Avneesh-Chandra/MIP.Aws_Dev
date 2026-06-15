# AWS Bedrock AI for MIP.Aws

MIP.Aws uses **Amazon Bedrock** as the production AI provider for PDF download recovery, selector suggestions, failed-source diagnosis, recovery explanations, status-email summaries, and operator suggested actions.

## Enable Bedrock

1. Open the [Amazon Bedrock console](https://console.aws.amazon.com/bedrock/) in your target region (default: `eu-north-1`).
2. Request access to the models you plan to use (see [Recommended models](#recommended-models)).
3. Set application configuration:

```json
"Ai": {
  "Provider": "AwsBedrock",
  "Enabled": true,
  "MockMode": false
},
"Aws": {
  "Region": "eu-north-1",
  "Bedrock": {
    "Enabled": true,
    "Region": "eu-north-1",
    "ModelId": "amazon.nova-lite-v1:0",
    "MaxTokens": 1200,
    "Temperature": 0.2,
    "TopP": 0.9,
    "TimeoutSeconds": 60
  }
}
```

Environment variable overrides (ECS, local shell, GitHub Actions):

| Variable | Maps to |
|----------|---------|
| `Ai__Provider` | `Ai:Provider` |
| `Ai__Enabled` | `Ai:Enabled` |
| `Ai__MockMode` | `Ai:MockMode` |
| `Aws__Region` | `Aws:Region` |
| `Aws__Bedrock__Enabled` | `Aws:Bedrock:Enabled` |
| `Aws__Bedrock__Region` | `Aws:Bedrock:Region` |
| `Aws__Bedrock__ModelId` | `Aws:Bedrock:ModelId` |
| `Aws__Bedrock__MaxTokens` | `Aws:Bedrock:MaxTokens` |
| `Aws__Bedrock__Temperature` | `Aws:Bedrock:Temperature` |
| `Aws__Bedrock__TopP` | `Aws:Bedrock:TopP` |
| `Aws__Bedrock__TimeoutSeconds` | `Aws:Bedrock:TimeoutSeconds` |

## Request model access

In Bedrock → **Model access**, enable:

- Amazon Nova Lite (default for **eu-north-1**)
- EU Claude Haiku 4.5 inference profile (`eu.anthropic.claude-haiku-4-5-20251001-v1:0`)
- Anthropic Claude 3.5 Sonnet (US regions / inference profiles only)

Access is per-region. Claude 3.5 Haiku (`anthropic.claude-3-5-haiku-20241022-v1:0`) is **not** available in eu-north-1.

## Recommended models

| Model ID | Use case |
|----------|----------|
| `amazon.nova-lite-v1:0` | **Default for eu-north-1** — fast, low cost |
| `eu.anthropic.claude-haiku-4-5-20251001-v1:0` | EU inference profile — Claude Haiku 4.5 |
| `anthropic.claude-3-5-haiku-20241022-v1:0` | US only (us-east-1 / us-west-2) |
| `amazon.nova-pro-v1:0` | Higher-quality operator wording in eu-north-1 |

## Local development

**Default:** `Ai:MockMode=true` or empty `Ai:Provider` in non-Production → `MockAiProvider` (no AWS calls).

To test Bedrock locally:

```powershell
$env:AWS_PROFILE = "your-profile"
$env:AWS_REGION = "eu-north-1"
```

Set in `appsettings.Development.json`:

```json
"Ai": { "Provider": "AwsBedrock", "Enabled": true, "MockMode": false },
"Aws": { "Bedrock": { "Enabled": true, "ModelId": "amazon.nova-lite-v1:0", "Region": "eu-north-1" } }
```

If Bedrock is not configured and `MockMode` is false, AI calls return a clear configuration error and recovery falls back to deterministic heuristics.

## ECS production (task role)

No Bedrock access keys are stored in the app. The ECS **task role** includes:

- `bedrock:InvokeModel`
- `bedrock:InvokeModelWithResponseStream`

Terraform variables: `enable_bedrock`, `bedrock_region`, `bedrock_model_id`.

**Least privilege (recommended later):** restrict IAM `resources` to specific model ARNs instead of `*`.

GitHub repository configuration:

| Name | Type | Purpose |
|------|------|---------|
| `AWS_REGION` | secret | Primary AWS region |
| `BEDROCK_REGION` | variable | Bedrock region (optional; defaults to `AWS_REGION`) |
| `BEDROCK_MODEL_ID` | variable | Model passed to Terraform / ECS env |

## Cost control

- Default Haiku model for recovery and summaries.
- `MaxTokens` capped at 1200.
- Recovery HTML truncated via `AiSourceRecovery:MaxHtmlChars`.
- Use `Ai:MockMode=true` in non-production environments.
- Monitor Bedrock usage in Cost Explorer and set AWS Budgets alerts.

## Troubleshooting

| Symptom | Likely cause | Action |
|---------|--------------|--------|
| `AccessDeniedException` on invoke | Model access not granted | Enable model in Bedrock console for the region |
| `ValidationException` / invalid payload | Wrong model family adapter | Use Claude (`anthropic.claude*`) or Nova (`amazon.nova*`) model IDs |
| Region not supported | Model unavailable in region | Switch `Aws:Bedrock:Region` or choose a supported model |
| Throttling (`ThrottlingException`) | Too many concurrent invokes | Reduce Hangfire AI workers; add retry/backoff |
| Empty AI response | Timeout or model error | Increase `TimeoutSeconds`; check CloudWatch logs |
| Recovery uses heuristics only | `Ai:Enabled=false`, Bedrock disabled, or invalid JSON | Check **Admin → AI provider** status page |

## Admin UI

**Admin → AI provider** (`/admin/ai-provider`) shows active provider, mock mode, Bedrock region/model, and last request status/error.

## Architecture

- `IAiTextGenerationService` — generic text/JSON generation
- `IAiRecoveryProvider` / `IAISourceRecoveryService` — download recovery
- `IAiProviderFactory` — selects Mock vs AwsBedrock
- `IBedrockPromptAdapter` — Claude vs Nova request payloads

AI safety: recovery patches never include credentials, compliance flags, or `IsDownloadAllowed`. Unsafe JSON is rejected; auto-recovery uses `AutoAiRecoveryPatchValidator`.

# Local AWS Bedrock Testing (MIP.Aws)

Test AI Download Recovery locally against your AWS account (**AviAws**, region **eu-north-1 / Stockholm**) before ECS deployment.

## Prerequisites

1. **Enable model access** in AWS Console:
   - [Amazon Bedrock](https://eu-north-1.console.aws.amazon.com/bedrock/) → **Model access**
   - Enable **Amazon Nova Lite** (`amazon.nova-lite-v1:0`) — default for **eu-north-1**
   - Or **EU Claude Haiku 4.5** inference profile (`eu.anthropic.claude-haiku-4-5-20251001-v1:0`)
   - **Do not use** Claude 3.5 Haiku (`anthropic.claude-3-5-haiku-20241022-v1:0`) in eu-north-1 — it is US-only

2. **Configure AWS CLI profile** (recommended):

```powershell
aws configure --profile mip-dev
```

| Prompt | Value |
|--------|-------|
| AWS Access Key ID | Your IAM user access key |
| AWS Secret Access Key | Your secret key |
| Default region | `eu-north-1` |
| Default output format | `json` |

3. **Verify credentials**:

```powershell
aws sts get-caller-identity --profile mip-dev
```

## Application configuration

Copy the template if you do not have a local Development file:

```powershell
Copy-Item src\MIP.Aws.Api\appsettings.Development.Template.json src\MIP.Aws.Api\appsettings.Development.json
```

Key settings in `appsettings.Development.json`:

```json
"Ai": {
  "Provider": "AwsBedrock",
  "Enabled": true,
  "MockMode": false
},
"Aws": {
  "Region": "eu-north-1",
  "Profile": "mip-dev",
  "Bedrock": {
    "Enabled": true,
    "Region": "eu-north-1",
    "ModelId": "amazon.nova-lite-v1:0"
  }
}
```

## Environment variables (PowerShell)

Override config without editing files:

```powershell
$env:AWS_PROFILE = "mip-dev"
$env:AWS_REGION = "eu-north-1"
$env:Ai__Provider = "AwsBedrock"
$env:Ai__Enabled = "true"
$env:Ai__MockMode = "false"
$env:Aws__Bedrock__Enabled = "true"
$env:Aws__Bedrock__Region = "eu-north-1"
$env:Aws__Bedrock__ModelId = "amazon.nova-lite-v1:0"
```

`AWS_PROFILE` takes precedence over `Aws:Profile` in config.

## Smoke test script

```powershell
.\scripts\test-bedrock-local.ps1
```

## Run MIP.Aws

```powershell
cd D:\MIPaws
dotnet run --project src\MIP.Aws.Api
```

Open:

- **AI settings:** http://localhost:5196/admin/ai-settings
- **API status:** `GET /api/v1/admin/ai/status` (ContentAdmin+)
- **API test:** `POST /api/v1/admin/ai/bedrock/test` (SuperAdmin only)

Login as `superadmin@mip.local` (password in your local `appsettings.Development.json`).

Click **Test Bedrock** on the AI settings page.

## Credential resolution order

The app uses the AWS SDK default chain:

1. `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` environment variables
2. `AWS_PROFILE` environment variable → shared credentials file (`~/.aws/credentials`)
3. `Aws:Profile` in configuration
4. Default profile / instance metadata (ECS task role in production)

**No access keys are stored in code or committed config.**

## AI features using local Bedrock

When `Ai:Provider=AwsBedrock` and `MockMode=false`:

- Manual AI recovery analysis
- Automatic AI download recovery
- Selector repair suggestions
- Download monitor status email AI summary (if mail enabled)
- Operator suggested interventions

If Bedrock fails, jobs do **not** crash — analysis falls back to deterministic heuristics unless `Ai:MockMode=true`.

## Troubleshooting

| Error | Fix |
|-------|-----|
| Profile not found | `aws configure --profile mip-dev` |
| Access denied | Enable model in Bedrock → Model access |
| Model not in region | Use `eu-north-1` and a supported model ID |
| Throttled | Retry; reduce concurrent recovery jobs |
| Mock mode active | Set `Ai:MockMode=false` |

See also [AWS_BEDROCK_AI.md](./AWS_BEDROCK_AI.md) for production ECS setup.

#Requires -Version 5.1
<#
.SYNOPSIS
  Checks local AWS CLI setup for MIP.Aws Bedrock testing.
#>
param(
    [string]$Profile = "mip-dev",
    [string]$Region = "eu-north-1"
)

$ErrorActionPreference = "Continue"

function Write-Status($label, $ok, $detail) {
    $icon = if ($ok) { "[OK]" } else { "[!!]" }
    Write-Host "$icon $label" -ForegroundColor $(if ($ok) { "Green" } else { "Yellow" })
    if ($detail) { Write-Host "    $detail" }
}

Write-Host "`nMIP.Aws — Local Bedrock setup check`n" -ForegroundColor Cyan

# AWS CLI
$aws = Get-Command aws -ErrorAction SilentlyContinue
if (-not $aws) {
    Write-Status "AWS CLI installed" $false "Install from https://aws.amazon.com/cli/"
    exit 1
}
Write-Status "AWS CLI installed" $true $aws.Source

# Caller identity
Write-Host "`nChecking credentials (profile: $Profile)...`n"
$identityJson = aws sts get-caller-identity --profile $Profile --output json 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Status "AWS credentials" $false "Run: aws configure --profile $Profile"
    Write-Host $identityJson
    exit 1
}

$identity = $identityJson | ConvertFrom-Json
Write-Status "AWS credentials" $true "Account $($identity.Account) / $($identity.Arn)"

# Region
$configuredRegion = aws configure get region --profile $Profile 2>$null
if ([string]::IsNullOrWhiteSpace($configuredRegion)) {
    $configuredRegion = "(not set on profile — use AWS_REGION=$Region)"
}
Write-Status "Configured region" ($configuredRegion -eq $Region -or $configuredRegion -like "*$Region*") $configuredRegion
Write-Host "    Expected Bedrock region: $Region"

# Bedrock model access hint
Write-Host "`nBedrock model access cannot be verified via CLI alone." -ForegroundColor DarkGray
Write-Host "Enable models in: AWS Console → Amazon Bedrock → Model access (eu-north-1)`n"

# Optional: list foundation models (requires bedrock:ListFoundationModels)
Write-Host "Listing foundation models (if permitted)...`n"
$models = aws bedrock list-foundation-models --region $Region --profile $Profile --output json 2>&1
if ($LASTEXITCODE -eq 0) {
    $parsed = $models | ConvertFrom-Json
    $haiku = $parsed.modelSummaries | Where-Object { $_.modelId -like "*claude-3-5-haiku*" }
    $nova = $parsed.modelSummaries | Where-Object { $_.modelId -like "*nova-lite*" }
    Write-Status "Claude Haiku listed" ($null -ne $haiku) $(if ($haiku) { $haiku[0].modelId } else { "Enable in Model access" })
    Write-Status "Nova Lite listed" ($null -ne $nova) $(if ($nova) { $nova[0].modelId } else { "optional fallback" })
} else {
    Write-Status "List foundation models" $false "Skipped (IAM may not include bedrock:ListFoundationModels — OK for app invoke)"
}

Write-Host "`nNext steps:" -ForegroundColor Cyan
Write-Host "  1. Set env:  `$env:AWS_PROFILE='$Profile'; `$env:AWS_REGION='$Region'"
Write-Host "  2. Run app:  dotnet run --project src\MIP.Aws.Api"
Write-Host "  3. Open:     http://localhost:5196/admin/ai-settings"
Write-Host "  4. Click:    Test Bedrock`n"

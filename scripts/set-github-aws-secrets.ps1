# Upload local mip-dev AWS credentials to GitHub repository secrets for AWS Deploy workflow.
# Prerequisite: gh auth login (once)

param(
    [string]$Profile = "mip-dev",
    [string]$Repo = "Avneesh-Chandra/MIP.Aws_Dev"
)

$ErrorActionPreference = "Stop"

gh auth status 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "GitHub CLI not logged in. Run: gh auth login" -ForegroundColor Yellow
    exit 1
}

$accessKey = (aws configure get aws_access_key_id --profile $Profile).Trim()
$secretKey = (aws configure get aws_secret_access_key --profile $Profile).Trim()

if ([string]::IsNullOrWhiteSpace($accessKey) -or [string]::IsNullOrWhiteSpace($secretKey)) {
    throw "AWS profile '$Profile' has no access keys. Run: aws configure --profile $Profile"
}

Write-Host "Setting repository secrets on $Repo (from profile $Profile)..."
$accessKey | gh secret set AWS_ACCESS_KEY_ID --repo $Repo
if ($LASTEXITCODE -ne 0) { throw "Failed to set AWS_ACCESS_KEY_ID" }
$secretKey | gh secret set AWS_SECRET_ACCESS_KEY --repo $Repo
if ($LASTEXITCODE -ne 0) { throw "Failed to set AWS_SECRET_ACCESS_KEY" }

Write-Host "Done. Re-run Actions -> AWS Deploy (apply_terraform=false, image_tag=latest)."

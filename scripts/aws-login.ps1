param(
    [string]$Region = $env:AWS_REGION,
    [string]$Profile = $(if ($env:AWS_PROFILE) { $env:AWS_PROFILE } else { "mip-dev" })
)

if ([string]::IsNullOrWhiteSpace($Region)) {
    $Region = "eu-north-1"
}

$awsArgs = @()
if (-not [string]::IsNullOrWhiteSpace($Profile)) { $awsArgs += @("--profile", $Profile) }

Write-Host "Verifying AWS CLI identity (profile: $Profile, region: $Region)..."
aws @awsArgs sts get-caller-identity --region $Region
if ($LASTEXITCODE -ne 0) {
    Write-Error "AWS CLI is not configured. Run: aws configure --profile $Profile"
    exit 1
}

Write-Host "AWS login verified."

param(
    [string]$Region = $env:AWS_REGION,
    [string]$AccountId = $env:AWS_ACCOUNT_ID,
    [string]$Environment = "dev",
    [string]$Tag = "latest",
    [string]$Profile = $(if ($env:AWS_PROFILE) { $env:AWS_PROFILE } else { "mip-dev" }),
    [switch]$ApiOnly
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($Region)) { $Region = "eu-north-1" }

$awsArgs = @()
if (-not [string]::IsNullOrWhiteSpace($Profile)) { $awsArgs += @("--profile", $Profile) }

Write-Host "Verifying AWS credentials..."
$identityJson = aws @awsArgs sts get-caller-identity --output json 2>&1
if ($LASTEXITCODE -ne 0) { throw "AWS auth failed. Run: aws configure --profile mip-dev`n$identityJson" }
$identity = $identityJson | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($AccountId)) { $AccountId = $identity.Account }
Write-Host "  Account: $AccountId  User: $($identity.Arn)"

$registry = "$AccountId.dkr.ecr.$Region.amazonaws.com"
$prefix = "mip-aws-$Environment"

Write-Host "Logging in to ECR $registry..."
aws @awsArgs ecr get-login-password --region $Region | docker login --username AWS --password-stdin $registry
if ($LASTEXITCODE -ne 0) { throw "ECR login failed." }

docker tag mip-aws-api:local "$registry/${prefix}-api:$Tag"
docker push "$registry/${prefix}-api:$Tag"
if ($LASTEXITCODE -ne 0) { throw "API image push failed." }
Write-Host "Pushed $registry/${prefix}-api:$Tag"

if (-not $ApiOnly) {
    docker tag mip-aws-worker:local "$registry/${prefix}-worker:$Tag"
    docker push "$registry/${prefix}-worker:$Tag"
    if ($LASTEXITCODE -ne 0) { throw "Worker image push failed." }
    Write-Host "Pushed $registry/${prefix}-worker:$Tag"
}

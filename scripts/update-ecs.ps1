param(
    [string]$Region = $env:AWS_REGION,
    [string]$Cluster = "mip-aws-dev-cluster",
    [string]$ApiService = "mip-aws-dev-api",
    [string]$WorkerService = "mip-aws-dev-worker",
    [string]$Profile = $(if ($env:AWS_PROFILE) { $env:AWS_PROFILE } else { "mip-dev" }),
    [switch]$ApiOnly
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($Region)) { $Region = "eu-north-1" }

$awsArgs = @()
if (-not [string]::IsNullOrWhiteSpace($Profile)) { $awsArgs += @("--profile", $Profile) }

Write-Host "Verifying AWS credentials..."
aws @awsArgs sts get-caller-identity --region $Region | Out-Null
if ($LASTEXITCODE -ne 0) { throw "AWS auth failed. Set AWS_PROFILE=mip-dev or run aws configure --profile mip-dev" }

Write-Host "Forcing new ECS deployment..."
aws @awsArgs ecs update-service --cluster $Cluster --service $ApiService --force-new-deployment --region $Region | Out-Null
if ($LASTEXITCODE -ne 0) { throw "ECS API service update failed." }
if (-not $ApiOnly) {
    aws @awsArgs ecs update-service --cluster $Cluster --service $WorkerService --force-new-deployment --region $Region | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "ECS worker service update failed." }
}
Write-Host "ECS services updated."

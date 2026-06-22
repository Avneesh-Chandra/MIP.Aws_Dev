# Run from YOUR PowerShell terminal (not the Cursor agent shell).
# Requires: Docker Desktop running, AWS profile mip-dev configured.
param(
    [string]$Region = "eu-north-1",
    [string]$Profile = "mip-dev",
    [string]$Tag = "latest",
    [switch]$SkipBuild,
    [switch]$TerraformApply
)

$ErrorActionPreference = "Stop"
Set-Location (Split-Path $PSScriptRoot -Parent)

. "$PSScriptRoot\Ensure-GitOnPath.ps1"
$null = Ensure-GitOnPath

Write-Host "=== MIP.Aws local deploy ===" -ForegroundColor Cyan

Write-Host "Checking Docker..."
docker info | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Docker is not reachable. Open Docker Desktop and retry in this terminal." }

if (-not $SkipBuild) {
    Write-Host "Building solution + Docker images..."
    & "$PSScriptRoot\build-images.ps1" -Configuration Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$env:AWS_PROFILE = $Profile
$env:AWS_REGION = $Region

Write-Host "Pushing images to ECR..."
& "$PSScriptRoot\push-ecr.ps1" -Tag $Tag -Profile $Profile -Region $Region
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Forcing ECS redeploy..."
& "$PSScriptRoot\update-ecs.ps1" -Profile $Profile -Region $Region
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($TerraformApply) {
    $terraform = Get-Command terraform -ErrorAction SilentlyContinue
    if (-not $terraform) {
        Write-Warning "Terraform not found on PATH - skipping infra apply."
        Write-Warning "Images were pushed and ECS was redeployed. API Auto AI settings are in appsettings.Production.json."
        Write-Warning "Install Terraform from https://developer.hashicorp.com/terraform/install then run:"
        Write-Host "  cd infra\terraform" -ForegroundColor Yellow
        Write-Host "  terraform init" -ForegroundColor Yellow
        Write-Host "  terraform apply" -ForegroundColor Yellow
        Write-Warning "Set worker_desired_count = 1 in deploy.auto.tfvars before apply (0 scales worker down)."
    }
    else {
        Write-Host "Applying Terraform (API Auto AI env vars)..."
        Push-Location infra\terraform
        & terraform init
        if ($LASTEXITCODE -ne 0) { Pop-Location; exit $LASTEXITCODE }
        & terraform apply -auto-approve
        if ($LASTEXITCODE -ne 0) { Pop-Location; exit $LASTEXITCODE }
        Pop-Location
    }
}

Write-Host "Done. Wait about 3 min for ECS tasks to stabilize, then run a PDF batch." -ForegroundColor Green

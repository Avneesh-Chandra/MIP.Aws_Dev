param(
    [string]$Configuration = "Release",
    [switch]$ApiOnly
)

$ErrorActionPreference = "Stop"
Set-Location (Split-Path $PSScriptRoot -Parent)

Write-Host "Building MIP.Aws solution ($Configuration)..."
dotnet build MIP.Aws.slnx -c $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Building Docker images..."
if (-not (Test-Path ".dockerignore")) {
    Write-Warning ".dockerignore missing - build context may be very large."
}
$prevEap = $ErrorActionPreference
$ErrorActionPreference = "Continue"
docker build -f Dockerfile.Api -t mip-aws-api:local .
if ($LASTEXITCODE -ne 0) { $ErrorActionPreference = $prevEap; exit $LASTEXITCODE }
if (-not $ApiOnly) {
    docker build -f Dockerfile.Worker -t mip-aws-worker:local .
    if ($LASTEXITCODE -ne 0) { $ErrorActionPreference = $prevEap; exit $LASTEXITCODE }
    Write-Host "Images: mip-aws-api:local, mip-aws-worker:local"
}
else {
    Write-Host "Images: mip-aws-api:local (worker skipped; ECS worker_desired_count=0)"
}
$ErrorActionPreference = $prevEap

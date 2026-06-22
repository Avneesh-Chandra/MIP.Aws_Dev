param(
    [string]$Configuration = "Release",
    [switch]$ApiOnly,
    [switch]$Slim
)

$ErrorActionPreference = "Stop"
Set-Location (Split-Path $PSScriptRoot -Parent)

. "$PSScriptRoot\Ensure-GitOnPath.ps1"
$null = Ensure-GitOnPath
$sourceRevision = Get-SourceRevision
Write-Host "Source revision: $sourceRevision" -ForegroundColor DarkGray

if ($Slim) {
    $publishDir = "artifacts/api-publish"
    Write-Host "Publishing API to $publishDir ($Configuration)..."
    dotnet publish src/MIP.Aws.Api/MIP.Aws.Api.csproj -c $Configuration -o $publishDir --no-self-contained
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
else {
    Write-Host "Building MIP.Aws solution ($Configuration)..."
    dotnet build MIP.Aws.slnx -c $Configuration
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "Building Docker images..."
if (-not (Test-Path ".dockerignore")) {
    Write-Warning ".dockerignore missing - build context may be very large."
}
$prevEap = $ErrorActionPreference
$ErrorActionPreference = "Continue"
$apiDockerfile = if ($Slim) { "Dockerfile.Api.slim" } else { "Dockerfile.Api" }
docker build -f $apiDockerfile -t mip-aws-api:local --build-arg "SOURCE_REVISION=$sourceRevision" .
if ($LASTEXITCODE -ne 0) { $ErrorActionPreference = $prevEap; exit $LASTEXITCODE }
if (-not $ApiOnly) {
    docker build -f Dockerfile.Worker -t mip-aws-worker:local --build-arg "SOURCE_REVISION=$sourceRevision" .
    if ($LASTEXITCODE -ne 0) { $ErrorActionPreference = $prevEap; exit $LASTEXITCODE }
    Write-Host "Images: mip-aws-api:local, mip-aws-worker:local"
}
else {
    Write-Host "Images: mip-aws-api:local (worker skipped; ECS worker_desired_count=0)"
}
$ErrorActionPreference = $prevEap

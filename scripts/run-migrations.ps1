param(
    [string]$ConnectionString = $env:ConnectionStrings__DefaultConnection
)

$ErrorActionPreference = "Stop"
Set-Location (Split-Path $PSScriptRoot -Parent)

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    Write-Error "Set ConnectionStrings__DefaultConnection or pass -ConnectionString"
    exit 1
}

$env:ConnectionStrings__DefaultConnection = $ConnectionString
dotnet ef database update --project src/MIP.Aws.Persistence --startup-project src/MIP.Aws.Api
Write-Host "Migrations applied."

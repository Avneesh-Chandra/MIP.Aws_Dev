#Requires -Version 5.1
<#
.SYNOPSIS
  Applies pending EF Core migrations to the configured DefaultConnection database.

.EXAMPLE
  .\scripts\Apply-DatabaseMigrations.ps1
#>
param(
    [string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = "Stop"
$Persistence = Join-Path $ProjectRoot "src\MIP.Aws.Persistence\MIP.Aws.Persistence.csproj"
$Api = Join-Path $ProjectRoot "src\MIP.Aws.Api\MIP.Aws.Api.csproj"

Write-Host "Applying EF Core migrations..." -ForegroundColor Cyan
dotnet ef database update --project $Persistence --startup-project $Api
Write-Host "Migrations applied." -ForegroundColor Green

#Requires -Version 5.1
<#
.SYNOPSIS
  First-time deploy of MIP.Aws to AviAws (eu-north-1) and link to myApplications MIP_Aws_Test.

.PREREQUISITES
  - AWS CLI v2 (aws configure --profile mip-dev)
  - Terraform >= 1.5
  - Docker Desktop
  - .NET 10 SDK

.EXAMPLE
  cd D:\MIPaws
  .\scripts\deploy-mip-aws-test.ps1
#>
param(
    [string]$Profile = "mip-dev",
    [string]$Region = "eu-north-1",
    [switch]$SkipTerraform,
    [switch]$SkipImages,
    [switch]$PlanOnly
)

$ErrorActionPreference = "Stop"
$Root = Split-Path $PSScriptRoot -Parent
$TfDir = Join-Path $Root "infra\terraform"
$TfVars = Join-Path $TfDir "terraform.tfvars"
$Example = Join-Path $TfDir "terraform.tfvars.avi-aws.example"

# Refresh PATH after winget installs (AWS CLI / Terraform)
$env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" +
            [System.Environment]::GetEnvironmentVariable("Path", "User")

function Resolve-ToolPath([string]$name) {
    $cmd = Get-Command $name -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $fallbacks = @(
        "C:\Program Files\Amazon\AWSCLIV2\aws.exe",
        (Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter "$name.exe" -ErrorAction SilentlyContinue |
            Select-Object -First 1).FullName
    ) | Where-Object { $_ -and (Test-Path $_) }
    if ($fallbacks.Count -gt 0) { return $fallbacks[0] }
    throw "Required command '$name' not found. Install via winget (Amazon.AWSCLI, Hashicorp.Terraform, Docker.DockerDesktop)."
}

$AwsExe = Resolve-ToolPath "aws"
$TerraformExe = Resolve-ToolPath "terraform"

function New-RandomPassword([int]$Length = 24) {
    $chars = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789!@#%"
    -join ((1..$Length) | ForEach-Object { $chars[(Get-Random -Maximum $chars.Length)] })
}

Write-Host "`n=== MIP.Aws deploy → MIP_Aws_Test (eu-north-1) ===`n" -ForegroundColor Cyan

if (-not $SkipImages) {
    try { Resolve-ToolPath "docker" | Out-Null }
    catch { throw "Docker is required for image build/push. Install Docker Desktop, then retry." }
}

$env:AWS_PROFILE = $Profile
$env:AWS_REGION = $Region
$env:TF_VAR_aws_region = $Region

Write-Host "Verifying AWS identity (profile: $Profile)..."
try {
    $identity = & $AwsExe sts get-caller-identity --profile $Profile --output json | ConvertFrom-Json
}
catch {
    Write-Host @"

AWS credentials not configured for profile '$Profile'.

Option A — access keys (IAM user):
  aws configure --profile $Profile
  # Region: $Region

Option B — IAM Identity Center (SSO):
  aws configure sso --profile $Profile

Then verify:
  aws sts get-caller-identity --profile $Profile

"@ -ForegroundColor Yellow
    throw
}
Write-Host "  Account: $($identity.Account)"
Write-Host "  ARN:     $($identity.Arn)"
if ($identity.Account -ne "640533249094") {
    Write-Warning "Expected AviAws account 640533249094. Continue only if intentional."
}

if (-not (Test-Path $TfVars)) {
    Write-Host "Creating terraform.tfvars from avi-aws example..."
    Copy-Item $Example $TfVars
    $dbPass = New-RandomPassword 28
    $jwtKey = New-RandomPassword 40
    (Get-Content $TfVars -Raw) `
        -replace 'CHANGE_ME_STRONG_PASSWORD_20_CHARS_MIN', $dbPass `
        -replace 'CHANGE_ME_MINIMUM_32_CHARACTER_JWT_SIGNING_KEY!!', $jwtKey `
        | Set-Content $TfVars -Encoding UTF8
    Write-Host "  Generated db_password and jwt_signing_key in terraform.tfvars (gitignored)."
    Write-Host "  Update ses_sender_email in terraform.tfvars if you need email."
}

if (-not $SkipTerraform) {
    Push-Location $TfDir
    try {
        & $TerraformExe init -input=false
        if ($PlanOnly) {
            & $TerraformExe plan
            Write-Host "`nPlan complete. Re-run without -PlanOnly to apply." -ForegroundColor Yellow
            return
        }
        Write-Host "`nApplying Terraform (RDS may take 20-40 minutes)..." -ForegroundColor Yellow
        & $TerraformExe apply -auto-approve
        $alb = & $TerraformExe output -raw alb_dns_name
        Write-Host "`nALB DNS: http://$alb" -ForegroundColor Green
        Write-Host "Update admin_portal_url in terraform.tfvars to http://$alb then re-apply if needed."
    }
    finally {
        Pop-Location
    }
}

if (-not $SkipImages -and -not $PlanOnly) {
    Push-Location $Root
    try {
        & (Join-Path $PSScriptRoot "build-images.ps1")
        $env:AWS_ACCOUNT_ID = $identity.Account
        & (Join-Path $PSScriptRoot "push-ecr.ps1") -Region $Region -AccountId $identity.Account
        & (Join-Path $PSScriptRoot "update-ecs.ps1") -Region $Region
        Write-Host "Waiting 90s for ECS tasks to start..."
        Start-Sleep -Seconds 90
        if ($alb) {
            try {
                Invoke-WebRequest -Uri "http://$alb/health/live" -UseBasicParsing -TimeoutSec 30 | Out-Null
                Write-Host "Health check OK: http://$alb/health/live" -ForegroundColor Green
            }
            catch {
                Write-Warning "Health check not ready yet. Check ECS logs in CloudWatch."
            }
        }
    }
    finally {
        Pop-Location
    }
}

Write-Host @"

=== Next steps ===
1. myApplications: open MIP_Aws_Test — resources with tag awsApplication should appear after sync (~30 min)
   https://eu-north-1.console.aws.amazon.com/console/applications/0cp7d3a9r0n4a7f512w9eup5jy

2. Run DB migrations (once RDS is available):
   .\scripts\run-migrations.ps1

3. Bedrock: enable Claude Haiku in Model access (eu-north-1)

4. SES: verify sender email for status emails

5. Open app: http://$alb
"@ -ForegroundColor Cyan

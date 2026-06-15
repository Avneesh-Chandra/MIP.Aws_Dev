param(
    [string]$Region = $env:AWS_REGION
)

$ErrorActionPreference = "Stop"
Set-Location (Join-Path (Split-Path $PSScriptRoot -Parent) "infra/terraform")
if ([string]::IsNullOrWhiteSpace($Region)) { $Region = "us-east-1" }
$env:TF_VAR_aws_region = $Region

terraform init
terraform plan

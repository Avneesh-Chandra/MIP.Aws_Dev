# Builds GFH-MIP-Aws-Optimized.xlsx - Test (Sandbox) + Production, ECS Fargate architecture.
# Target: ~USD 400 / month (~BHD 150). Requires Microsoft Excel (COM).

$ErrorActionPreference = 'Stop'
$sourcePath = 'd:\Avi-alm\MIP\GFH-MIP.xlsx'
$outPath = 'd:\Avi-alm\MIP\GFH-MIP-AWS-updated-Optimised.xlsx'
$fxRate = 0.376
$targetMonthlyUsd = 400.00

# ECS task: 512 CPU / 2048 MB, ASPNETCORE_ENVIRONMENT=Production (hardcoded in ecs module for all envs).
# HTTPS: included in estimate for both envs; live Terraform ALB is HTTP-only until ACM is added.

$testRows = @(
    @{ Env = 'Sandbox (Test)'; Desc = 'Amazon RDS'; Config = 'SQL Server Express, db.t3.micro, Single-AZ, 20 GB gp3, 1-day backup.'; Usd = 52.00 }
    @{ Env = ''; Desc = 'Amazon ECS Fargate (API)'; Config = '1 task: 512 CPU (0.5 vCPU), 2048 MB, 24x7. ASPNETCORE_ENVIRONMENT=Production.'; Usd = 24.00 }
    @{ Env = ''; Desc = 'Amazon ECS Fargate (Worker)'; Config = '0 tasks (worker_desired_count=0). Downloads run in API container.'; Usd = 0.00 }
    @{ Env = ''; Desc = 'Application Load Balancer + ACM'; Config = '1 ALB, HTTPS (443) + HTTP redirect, ACM cert on custom domain.'; Usd = 22.00 }
    @{ Env = ''; Desc = 'Amazon Route 53'; Config = 'Hosted zone + alias record for test HTTPS URL.'; Usd = 1.00 }
    @{ Env = ''; Desc = 'Amazon S3'; Config = '25 GB Standard (PDFs + versioning). Lifecycle to IA after 30 days.'; Usd = 2.50 }
    @{ Env = ''; Desc = 'Amazon CloudWatch Logs'; Config = '7-day retention (api, worker, hangfire log groups).'; Usd = 5.00 }
    @{ Env = ''; Desc = 'AWS Secrets Manager'; Config = 'JWT + connection strings for test environment.'; Usd = 1.20 }
    @{ Env = ''; Desc = 'Amazon SES'; Config = 'Status emails with https:// AdminPortalUrl links.'; Usd = 0.50 }
    @{ Env = ''; Desc = 'Amazon Bedrock (Auto AI)'; Config = 'amazon.nova-lite-v1:0, light recovery (~100 calls/month).'; Usd = 5.00 }
    @{ Env = ''; Desc = 'Data transfer + VPC'; Config = 'ALB HTTPS egress, PDF downloads. NAT disabled.'; Usd = 6.00 }
)

$prodRows = @(
    @{ Env = 'Production'; Desc = 'Amazon RDS'; Config = 'SQL Server Express, db.t3.small, Single-AZ, 50 GB gp3, 7-day backup.'; Usd = 198.00 }
    @{ Env = ''; Desc = 'Amazon ECS Fargate (API)'; Config = '1 task: 512 CPU (0.5 vCPU), 2048 MB, 24x7. ASPNETCORE_ENVIRONMENT=Production. Same as Test.'; Usd = 24.00 }
    @{ Env = ''; Desc = 'Amazon ECS Fargate (Worker)'; Config = '0 tasks initially. Scale to 1 task later if needed (+~$24/mo at same size).'; Usd = 0.00 }
    @{ Env = ''; Desc = 'Application Load Balancer + ACM'; Config = '1 ALB, HTTPS (443) + HTTP redirect, ACM cert on custom domain.'; Usd = 22.00 }
    @{ Env = ''; Desc = 'Amazon Route 53'; Config = 'Hosted zone + alias record for production HTTPS URL.'; Usd = 1.00 }
    @{ Env = ''; Desc = 'Amazon S3'; Config = '80 GB Standard archive + versioning. Lifecycle to Glacier after 90 days.'; Usd = 6.00 }
    @{ Env = ''; Desc = 'Amazon CloudWatch Logs'; Config = '14-day retention, higher Hangfire / download volume.'; Usd = 10.00 }
    @{ Env = ''; Desc = 'AWS Secrets Manager'; Config = 'JWT + DB secrets for production.'; Usd = 1.50 }
    @{ Env = ''; Desc = 'Amazon SES'; Config = 'Production mail (~500 emails/month status + alerts).'; Usd = 2.00 }
    @{ Env = ''; Desc = 'Amazon Bedrock (Auto AI)'; Config = 'nova-lite, moderate recovery (~300-500 calls/month).'; Usd = 12.00 }
    @{ Env = ''; Desc = 'Data transfer'; Config = 'Higher PDF egress over HTTPS. NAT still disabled.'; Usd = 12.00 }
)

$sharedRows = @(
    @{ Env = 'Shared (both envs)'; Desc = 'Amazon ECR'; Config = '3 GB container images (API + Worker). One registry, two image tags per env.'; Usd = 2.00 }
    @{ Env = ''; Desc = 'Contingency buffer'; Config = 'RDS credits, log spikes, Bedrock variance (~3%).'; Usd = 4.00 }
)

$removedRows = @(
    @{ Env = 'Not used (vs original GFH-MIP.xlsx)'; Desc = 'Amazon EKS'; Config = 'MIP.Aws uses ECS Fargate only. Was $73 + $93/month.'; Usd = 0.00 }
    @{ Env = ''; Desc = 'EC2 instances (API / Worker)'; Config = 'Replaced by Fargate tasks. Was $25 + $248/month.'; Usd = 0.00 }
    @{ Env = ''; Desc = 'ElastiCache Redis'; Config = 'Application uses in-memory cache; not in Terraform.'; Usd = 0.00 }
    @{ Env = ''; Desc = 'Amazon Textract / OCR'; Config = 'Not in current MIP.Aws PDF download architecture.'; Usd = 0.00 }
    @{ Env = ''; Desc = 'AWS Business Support Plan'; Config = 'Optional; excluded from GFH pilot budget.'; Usd = 0.00 }
    @{ Env = ''; Desc = 'NAT Gateway'; Config = 'Disabled in Terraform for cost control (~$32/mo saved per env).'; Usd = 0.00 }
    @{ Env = ''; Desc = 'RDS SQL Server Standard (db.m6i.large)'; Config = 'Deferred; Express on t3.small sufficient for initial prod.'; Usd = 0.00 }
)

$allRows = $testRows + $prodRows + $sharedRows + $removedRows
$monthlyUsd = [math]::Round(($allRows | Where-Object { $_.Usd -gt 0 } | ForEach-Object { $_.Usd } | Measure-Object -Sum).Sum, 2)
$yearlyUsd = [math]::Round($monthlyUsd * 12, 2)
$yearlyBhd = [math]::Round($yearlyUsd * $fxRate, 2)
$monthlyBhd = [math]::Round($monthlyUsd * $fxRate, 2)

$testSubtotal = [math]::Round(($testRows | Where-Object { $_.Usd -gt 0 } | ForEach-Object { $_.Usd } | Measure-Object -Sum).Sum, 2)
$prodSubtotal = [math]::Round(($prodRows | Where-Object { $_.Usd -gt 0 } | ForEach-Object { $_.Usd } | Measure-Object -Sum).Sum, 2)

if (-not (Test-Path $sourcePath)) { throw "Source workbook not found: $sourcePath" }
$templatePath = $sourcePath
if (Test-Path $outPath) {
    $templatePath = $outPath
} elseif (-not (Test-Path $sourcePath)) {
    throw "No template workbook available"
}
if ($templatePath -eq $sourcePath) {
    Copy-Item -Path $sourcePath -Destination $outPath -Force -ErrorAction Stop
}

$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
try {
    $wb = $excel.Workbooks.Open($outPath)
    $ws = $wb.Worksheets.Item('My Estimate')

    $lastDataRow = 3 + $allRows.Count
    $clearEndRow = [Math]::Max(21, $lastDataRow)
    foreach ($addr in @("A4:E$clearEndRow", 'A23:E25')) {
        try { $ws.Range($addr).UnMerge() } catch { }
    }
    $ws.Range("A4:E$clearEndRow").ClearContents()

    $r = 4
    $sn = 1
    $lastEnv = ''
    foreach ($item in $allRows) {
        $envLabel = if ($item.Env -and $item.Env -ne $lastEnv) { $item.Env } else { $null }
        if ($item.Env) { $lastEnv = $item.Env }

        $ws.Cells.Item($r, 1).Value2 = [double]$sn
        if ($envLabel) { $ws.Cells.Item($r, 2).Value2 = [string]$envLabel }
        $ws.Cells.Item($r, 3).Value2 = [string]$item.Desc
        $ws.Cells.Item($r, 4).Value2 = [double]$item.Usd
        $ws.Cells.Item($r, 5).Value2 = [string]$item.Config
        $sn++
        $r++
    }

    $totalRow = $lastDataRow + 2
    $ws.Cells.Item($totalRow, 1).Value2 = 'Total Monthly Estimate (USD) - Test + Production'
    $ws.Cells.Item($totalRow, 4).Value2 = [double]$monthlyUsd
    $ws.Cells.Item($totalRow + 1, 1).Value2 = 'Annual estimate (USD)'
    $ws.Cells.Item($totalRow + 1, 4).Value2 = [double]$yearlyUsd
    $ws.Cells.Item($totalRow + 2, 1).Value2 = 'Annual estimate (BHD @ 0.376)'
    $ws.Cells.Item($totalRow + 2, 4).Value2 = [double]$yearlyBhd
    $ws.Cells.Item($totalRow + 3, 1).Value2 = 'Monthly estimate (BHD @ 0.376)'
    $ws.Cells.Item($totalRow + 3, 4).Value2 = [double]$monthlyBhd
    $ws.Cells.Item(1, 1).Value2 = 'MIP.Aws - Test (Sandbox) + Production Estimate (ECS Fargate)'
    $ws.Columns.Item('E').ColumnWidth = 78

    $origMonthly = 2016.73
    $saving = [math]::Round($origMonthly - $monthlyUsd, 2)
    $savingPct = [math]::Round(100 * $saving / $origMonthly, 1)

    $notes = $null
    foreach ($s in @($wb.Worksheets)) {
        if ($s.Name -eq 'Optimization Notes') { $notes = $s; break }
    }
    if ($null -eq $notes) {
        $notes = $wb.Worksheets.Add([Type]::Missing, $wb.Worksheets.Item($wb.Worksheets.Count))
        $notes.Name = 'Optimization Notes'
    }
    $notes.Cells.Clear()
    $notes.Range('A1').Value2 = 'MIP.Aws Test + Production - Cost Summary'
    $notes.Range('A3').Value2 = "Previous optimized sheet covered DEV ONLY (~USD 102/mo). This revision covers BOTH environments."
    $notes.Range('A4').Value2 = "Sandbox (Test) subtotal: USD $testSubtotal / month"
    $notes.Range('A5').Value2 = "Production subtotal: USD $prodSubtotal / month"
    $notes.Range('A6').Value2 = "Combined total: USD $monthlyUsd / month (BHD $monthlyBhd / month, BHD $yearlyBhd / year)"
    $notes.Range('A7').Value2 = "Target budget: ~USD $targetMonthlyUsd (~BHD $([math]::Round($targetMonthlyUsd * $fxRate, 2))) / month"
    $notes.Range('A9').Value2 = 'Alignment with current MIP.Aws Terraform (D:\MIPaws\infra\terraform):'
    $notes.Range('A10').Value2 = 'INCLUDED: VPC, ALB, ECS Fargate (API + Worker service), RDS SQL Server, S3, ECR,'
    $notes.Range('A11').Value2 = 'CloudWatch Logs, Secrets Manager, SES, Bedrock IAM (optional AI recovery).'
    $notes.Range('A12').Value2 = 'NOT INCLUDED: EKS, EC2 nodes, ElastiCache Redis, Textract/OCR, NAT Gateway, Business Support.'
    $notes.Range('A14').Value2 = 'ECS task (both envs): 512 CPU (0.5 vCPU), 2048 MB, ASPNETCORE_ENVIRONMENT=Production.'
    $notes.Range('A15').Value2 = 'HTTPS (both envs): estimate includes ACM (free) + Route 53 + ALB HTTPS listener.'
    $notes.Range('A16').Value2 = 'LIVE TODAY: Test ALB is HTTP only (no ACM listener in Terraform yet). HTTPS requires infra change.'
    $notes.Range('A17').Value2 = 'Set admin_portal_url to https://... and Auth__UseHttpsCookies=true after HTTPS is enabled.'
    $notes.Range('A18').Value2 = 'Playwright publisher blocks (e.g. Cloudflare) are separate from portal HTTPS; egress IP may still matter.'
    $notes.Range('A19').Value2 = 'Update deploy.auto.tfvars: api_cpu=512, api_memory=2048 to match ECS console if not already applied.'
    $notes.Range('A21').Value2 = 'PressReader / publisher licenses: budget separately (non-AWS).'
    $notes.Columns.Item('A').ColumnWidth = 115

    $wb.Save()
    $wb.Close($false)
    Write-Host "Created: $outPath"
    Write-Host "Test: $testSubtotal | Prod: $prodSubtotal | Total USD: $monthlyUsd | BHD/mo: $monthlyBhd"
}
finally {
    $excel.Quit()
    [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel)
}

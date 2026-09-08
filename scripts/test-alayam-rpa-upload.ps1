param(
    [Parameter(Mandatory = $true)]
    [string]$BaseUrl,
    [Parameter(Mandatory = $true)]
    [string]$ApiKey,
    [Parameter(Mandatory = $true)]
    [Guid]$SourceId,
    [Parameter(Mandatory = $true)]
    [string]$PdfPath,
    [bool]$EnqueueOcr = $true
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd("/")

if (-not (Test-Path -LiteralPath $PdfPath)) {
    throw "PDF not found: $PdfPath"
}

$url = "$BaseUrl/api/v1/news-sources/$SourceId/upload-today-pdf?enqueueOcr=$EnqueueOcr"
Write-Host "POST $url"

$curl = Get-Command curl.exe -ErrorAction SilentlyContinue
if ($curl) {
    & curl.exe -sS -w "`nHTTP %{http_code}`n" `
        -H "X-Rpa-Api-Key: $ApiKey" `
        -F "file=@$PdfPath;type=application/pdf" `
        $url
    if ($LASTEXITCODE -ne 0) {
        throw "curl failed with exit code $LASTEXITCODE"
    }
} else {
    $form = @{ file = Get-Item -LiteralPath $PdfPath }
    Invoke-RestMethod -Method Post -Uri $url -Headers @{ "X-Rpa-Api-Key" = $ApiKey } -Form $form | ConvertTo-Json -Depth 6
}


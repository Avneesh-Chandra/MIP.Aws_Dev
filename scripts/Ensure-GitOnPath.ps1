# Ensures git.exe is on PATH for MSBuild / Docker build metadata.
# GitHub Desktop ships git but often does not register it in the user PATH.

function Ensure-GitOnPath {
    if (Get-Command git -ErrorAction SilentlyContinue) {
        return $true
    }

    $patterns = @(
        (Join-Path $env:LOCALAPPDATA "GitHubDesktop\app-*\resources\app\git\cmd"),
        "C:\Program Files\Git\cmd",
        "C:\Program Files (x86)\Git\cmd"
    )

    foreach ($pattern in $patterns) {
        $dirs = @(Get-ChildItem -Path $pattern -Directory -ErrorAction SilentlyContinue |
            Sort-Object { $_.FullName } -Descending)
        foreach ($dir in $dirs) {
            $gitExe = Join-Path $dir.FullName "git.exe"
            if (Test-Path -LiteralPath $gitExe) {
                $env:PATH = "$($dir.FullName);$env:PATH"
                Write-Host "Using Git from $($dir.FullName)" -ForegroundColor DarkGray
                return $true
            }
        }
    }

    Write-Warning @"
git.exe was not found on PATH.
Install Git for Windows (https://git-scm.com/download/win) and choose "Git from the command line",
or ensure GitHub Desktop is installed. Builds will continue but commit metadata may be missing.
"@
    return $false
}

function Get-SourceRevision {
    param(
        [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent),
        [string]$Default = "unknown"
    )

    if (-not (Ensure-GitOnPath)) {
        return $Default
    }

    try {
        $sha = & git -C $RepoRoot rev-parse --short HEAD 2>$null
        if ($LASTEXITCODE -eq 0 -and $sha) {
            return $sha.Trim()
        }
    }
    catch {
        # fall through
    }

    return $Default
}

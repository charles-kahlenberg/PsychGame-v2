<#
.SYNOPSIS
  Downloads every research session's log data as its own readable .json file.

.DESCRIPTION
  Fetches the session list from the psych-log Worker, then saves one
  "session_<id>.json" file per session into the output folder. No URLs
  to type by hand - just run this script.

.PARAMETER Key
  The export key (ask Charlie / check your password manager if you don't
  have it). You can also set it once via:
    $env:PSYCH_EXPORT_KEY = "your-key-here"
  so you don't have to pass -Key every time.

.PARAMETER OutDir
  Folder to save the files into. Defaults to .\exports next to this script.

.EXAMPLE
  .\export-sessions.ps1 -Key "MGVin6EdMRSQpdH2LnR-cLML5IANcE_1"

.EXAMPLE
  $env:PSYCH_EXPORT_KEY = "MGVin6EdMRSQpdH2LnR-cLML5IANcE_1"
  .\export-sessions.ps1
#>
param(
    [string]$Key = $env:PSYCH_EXPORT_KEY,
    [string]$OutDir = (Join-Path $PSScriptRoot "exports")
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Key)) {
    Write-Error "No export key provided. Pass -Key '...' or set `$env:PSYCH_EXPORT_KEY first."
    exit 1
}

$WorkerUrl = "https://psych-log.charliekahlenberg.workers.dev"

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

Write-Host "Fetching session list..."
$list = Invoke-RestMethod -Uri "$WorkerUrl/export/sessions?key=$Key" -Method Get
$sessions = $list.sessions

if (-not $sessions -or $sessions.Count -eq 0) {
    Write-Host "No sessions found."
    exit 0
}

Write-Host "Found $($sessions.Count) session(s). Downloading to $OutDir ..."

foreach ($s in $sessions) {
    $sid = $s.session_id
    $destPath = Join-Path $OutDir "session_$sid.json"

    Invoke-RestMethod -Uri "$WorkerUrl/export/session/$sid`?key=$Key" -Method Get -OutFile $destPath
    Write-Host "  saved $($s.click_count) click(s) -> session_$sid.json"
}

Write-Host ""
Write-Host "Done. Open any file in $OutDir with Notepad, VS Code, etc."

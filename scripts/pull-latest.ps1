<#
.SYNOPSIS
    Pulls the latest PsychGame changes from GitHub onto this machine.

.DESCRIPTION
    Run this on your laptop (or any machine with the repo already cloned) to
    sync it with origin/main. Close Unity before running this so it doesn't
    lock files mid-pull.
#>

$ErrorActionPreference = "Stop"

# Always operate from the repo root, regardless of where the script is invoked from.
$repoRoot = git -C $PSScriptRoot rev-parse --show-toplevel 2>$null
if (-not $repoRoot) {
    Write-Error "This doesn't look like a git repository. Run this script from inside the PsychGame repo."
    exit 1
}
Set-Location $repoRoot

Write-Host "Repo: $repoRoot" -ForegroundColor Cyan

$branch = git rev-parse --abbrev-ref HEAD
Write-Host "Branch: $branch" -ForegroundColor Cyan

# Warn about local changes instead of silently stashing/losing them.
$status = git status --porcelain
if ($status) {
    Write-Host ""
    Write-Host "You have uncommitted local changes:" -ForegroundColor Yellow
    git status --short
    Write-Host ""
    $answer = Read-Host "Stash them and continue with the pull? (y/N)"
    if ($answer -eq "y" -or $answer -eq "Y") {
        git stash push -u -m "pull-latest.ps1 auto-stash $(Get-Date -Format o)"
        $stashed = $true
    } else {
        Write-Host "Aborting so you don't lose local work. Commit or stash manually, then re-run." -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "Fetching from origin..." -ForegroundColor Cyan
git fetch origin

Write-Host "Pulling latest $branch..." -ForegroundColor Cyan
git pull origin $branch

if ($stashed) {
    Write-Host ""
    Write-Host "Restoring your stashed changes..." -ForegroundColor Cyan
    git stash pop
}

Write-Host ""
Write-Host "Done. Latest commit:" -ForegroundColor Green
git log -1 --oneline

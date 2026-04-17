#Requires -Version 5.1
<#
.SYNOPSIS
Migrate Ashare production data to local SQLite and run Ashare.Api on it.

.DESCRIPTION
Run from the repo root. Steps:
  1) Build and run AshareMigrator (reads SQL Server, writes SQLite)
  2) Copy the migrated file to data/ashare-platform.db (official Ashare.Api path)
  3) Set ASHARE_SKIP_SCHEMA_GUARD=true so SchemaGuard does not wipe it
  4) Run Ashare.Api

.EXAMPLE
.\tools\AshareMigrator\run-migration.ps1
#>

$ErrorActionPreference = "Stop"

# Repo root = script-folder/../..
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $repoRoot

$migratedFile = Join-Path $repoRoot "tools\AshareMigrator\data\ashare-migrated.db"
$ashareDbFile = Join-Path $repoRoot "data\ashare-platform.db"
$backupDbFile = Join-Path $repoRoot "data\ashare-platform.seed-backup.db"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Ashare migration -> Ashare.Api runtime " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 1) Run migrator (truncate for clean start)
Write-Host "`n[1/4] Running AshareMigrator..." -ForegroundColor Yellow
& dotnet run --project tools\AshareMigrator -- --truncate true
if ($LASTEXITCODE -ne 0) { throw "Migration failed (exit $LASTEXITCODE)" }

if (-not (Test-Path $migratedFile)) {
    throw "Migrated file was not created at: $migratedFile"
}

# 2) Backup + replace
Write-Host "`n[2/4] Copying migrated file to data\ashare-platform.db..." -ForegroundColor Yellow
if (Test-Path $ashareDbFile) {
    if (-not (Test-Path $backupDbFile)) {
        Copy-Item $ashareDbFile $backupDbFile
        Write-Host "    Backup saved: $backupDbFile"
    }
    # Remove WAL/SHM files in case of open connections
    Remove-Item "$ashareDbFile-wal" -ErrorAction SilentlyContinue
    Remove-Item "$ashareDbFile-shm" -ErrorAction SilentlyContinue
    Remove-Item $ashareDbFile
}
Copy-Item $migratedFile $ashareDbFile
Write-Host "    Copied to: $ashareDbFile"

# 3) Environment variables
Write-Host "`n[3/4] Setting env vars..." -ForegroundColor Yellow
$env:ASHARE_SKIP_SCHEMA_GUARD = "true"
$env:ASPNETCORE_ENVIRONMENT = "Development"
Write-Host "    ASHARE_SKIP_SCHEMA_GUARD=true"

# 4) Run Ashare.Api
Write-Host "`n[4/4] Starting Ashare.Api..." -ForegroundColor Yellow
Write-Host "    Watch the log for 'SQLite DB path' and 'Existing listings before seed'" -ForegroundColor Gray
Write-Host "    If Existing listings = 17 the migration succeeded" -ForegroundColor Gray
Write-Host ""
& dotnet run --project Apps\Ashare\Customer\Backend\Ashare.Api

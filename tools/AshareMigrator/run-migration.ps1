#Requires -Version 5.1
<#
.SYNOPSIS
Migrates Ashare production data to local SQLite and prepares Ashare.Api to serve it.

.DESCRIPTION
يعمل هذا السكريبت من جذر المستودع:
  1) يبني AshareMigrator ويشغّله لقراءة البيانات من SQL Server وكتابتها إلى SQLite
  2) ينسخ الملف المُرحَّل إلى data/ashare-platform.db (مسار Ashare.Api الرسمي)
  3) يُفعّل متغير البيئة ASHARE_SKIP_SCHEMA_GUARD حتى لا يحذف SchemaGuard الملف
  4) يشغّل Ashare.Api

.EXAMPLE
.\tools\AshareMigrator\run-migration.ps1
#>

$ErrorActionPreference = "Stop"

# جذر الريبو = مجلد السكريبت/../..
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $repoRoot

$migratedFile   = Join-Path $repoRoot "tools\AshareMigrator\data\ashare-migrated.db"
$ashareDbFile   = Join-Path $repoRoot "data\ashare-platform.db"
$backupDbFile   = Join-Path $repoRoot "data\ashare-platform.seed-backup.db"

Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host " ترحيل بيانات عشير → تشغيل Ashare.Api" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

# 1) تشغيل المُرحِّل (--truncate لبدء نظيف)
Write-Host "`n[1/4] تشغيل AshareMigrator..." -ForegroundColor Yellow
& dotnet run --project tools\AshareMigrator -- --truncate true
if ($LASTEXITCODE -ne 0) { throw "فشل الترحيل (exit $LASTEXITCODE)" }

if (-not (Test-Path $migratedFile)) {
    throw "الملف المُرحَّل لم يُنشأ: $migratedFile"
}

# 2) نسخ احتياطي + استبدال
Write-Host "`n[2/4] نسخ الملف إلى data\ashare-platform.db..." -ForegroundColor Yellow
if (Test-Path $ashareDbFile) {
    if (-not (Test-Path $backupDbFile)) {
        Copy-Item $ashareDbFile $backupDbFile
        Write-Host "    نسخة احتياطية: $backupDbFile"
    }
    # أوقف أي اتصالات مفتوحة قبل الحذف
    Remove-Item "$ashareDbFile-wal" -ErrorAction SilentlyContinue
    Remove-Item "$ashareDbFile-shm" -ErrorAction SilentlyContinue
    Remove-Item $ashareDbFile
}
Copy-Item $migratedFile $ashareDbFile
Write-Host "    نُسخ: $ashareDbFile"

# 3) حذف البصمة القديمة من ملف الهدف (إن وُجدت) — نحن على مخطط مختلف
Write-Host "`n[3/4] إعداد متغيرات البيئة..." -ForegroundColor Yellow
$env:ASHARE_SKIP_SCHEMA_GUARD = "true"
$env:ASPNETCORE_ENVIRONMENT = "Development"
Write-Host "    ASHARE_SKIP_SCHEMA_GUARD=true"

# 4) تشغيل Ashare.Api
Write-Host "`n[4/4] تشغيل Ashare.Api..." -ForegroundColor Yellow
Write-Host "    راقب اللوج: 'SQLite DB path' و 'Existing listings before seed'" -ForegroundColor Gray
Write-Host "    إذا ظهر Existing listings = 17 → الترحيل نجح" -ForegroundColor Gray
Write-Host ""
& dotnet run --project Apps\Ashare\Customer\Backend\Ashare.Api

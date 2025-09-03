#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Sets up .NET Aspire for the Blazor Fishing Regulations application.

.DESCRIPTION
    This script provides a simple interface to set up .NET Aspire development environment
    for the Blazor AI Fishing Regulations application.

.PARAMETER SkipWorkloadInstall
    Skip installing the Aspire workload (if already installed)

.EXAMPLE
    .\setup.ps1
    Install Aspire workload and set up environment

.EXAMPLE
    .\setup.ps1 -SkipWorkloadInstall
    Set up environment without installing workload
#>

param(
    [switch]$SkipWorkloadInstall
)

$ErrorActionPreference = "Stop"

Write-Host @"
🚀 ============================================================= 🚀
   Blazor AI Fishing Regulations - .NET Aspire Setup
🚀 ============================================================= 🚀
"@ -ForegroundColor Cyan

# Get the script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir

Write-Host "`n📁 Working directory: $rootDir" -ForegroundColor Gray

# Change to root directory
Push-Location $rootDir

try {
    Write-Host "`n🔧 Setting up .NET Aspire..." -ForegroundColor Cyan
    
    # Run Aspire setup
    if ($SkipWorkloadInstall) {
        & "$scriptDir\setup-aspire.ps1" -SkipWorkloadInstall
    } else {
        & "$scriptDir\setup-aspire.ps1"
    }
    
    if ($LASTEXITCODE -ne 0) {
        throw "Aspire setup failed"
    }
    
    Write-Host "`n🎉 SUCCESS! .NET Aspire setup completed successfully!" -ForegroundColor Green
    
    Write-Host "`n🚀 NEXT STEPS:" -ForegroundColor Cyan
    Write-Host "==============" -ForegroundColor Cyan
    Write-Host "1. Start Aspire application:" -ForegroundColor Yellow
    Write-Host "   dotnet run --project src\FishingRegs.AppHost" -ForegroundColor Gray
    Write-Host ""
    Write-Host "2. Open Aspire Dashboard:" -ForegroundColor Yellow
    Write-Host "   http://localhost:15888 (opens automatically)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "3. Access services through dashboard:" -ForegroundColor Yellow
    Write-Host "   • All services managed by Aspire" -ForegroundColor Gray
    Write-Host "   • Built-in health checks and monitoring" -ForegroundColor Gray
    Write-Host "   • Automatic service discovery" -ForegroundColor Gray
    Write-Host ""
    Write-Host "📝 NOTE: Using .NET Aspire orchestration" -ForegroundColor Blue
    Write-Host "   All services (SQL Server, Redis, Storage, etc.) are" -ForegroundColor Blue
    Write-Host "   automatically managed through the Aspire dashboard." -ForegroundColor Blue
    
} catch {
    Write-Host "`n❌ Setup failed: $_" -ForegroundColor Red
    Write-Host "`n🔧 TROUBLESHOOTING:" -ForegroundColor Yellow
    Write-Host "==================" -ForegroundColor Yellow
    Write-Host "1. Check the detailed error message above" -ForegroundColor Gray
    Write-Host "2. Ensure you have required permissions" -ForegroundColor Gray
    Write-Host "3. Verify .NET 8.0+ is installed (dotnet --version)" -ForegroundColor Gray
    Write-Host "4. Check network connectivity for downloading workload" -ForegroundColor Gray
    Write-Host "5. Run: dotnet workload list to see installed workloads" -ForegroundColor Gray
    
    exit 1
} finally {
    Pop-Location
}

Write-Host "`n📚 For more information about .NET Aspire:" -ForegroundColor Cyan
Write-Host "   https://learn.microsoft.com/en-us/dotnet/aspire/" -ForegroundColor Gray

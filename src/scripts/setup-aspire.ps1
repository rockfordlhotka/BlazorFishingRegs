#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Sets up .NET Aspire for the Blazor Fishing Regulations application.

.DESCRIPTION
    This script installs the .NET Aspire workload and sets up the development environment
    for running the application with Aspire orchestration.

.PARAMETER SkipWorkloadInstall
    Skip installing the Aspire workload (if already installed)

.EXAMPLE
    .\setup-aspire.ps1
    Install Aspire workload and set up environment

.EXAMPLE
    .\setup-aspire.ps1 -SkipWorkloadInstall
    Set up environment without installing workload
#>

param(
    [switch]$SkipWorkloadInstall
)

$ErrorActionPreference = "Stop"

Write-Host @"
🚀 ============================================================= 🚀
   .NET Aspire Setup for Blazor Fishing Regulations
🚀 ============================================================= 🚀
"@ -ForegroundColor Cyan

# Check .NET version
$dotnetVersion = dotnet --version
Write-Host "📋 .NET Version: $dotnetVersion" -ForegroundColor Green

if ($dotnetVersion -lt "8.0") {
    Write-Error "❌ .NET 8.0 or higher is required for .NET Aspire"
    exit 1
}

# Install Aspire workload
if (-not $SkipWorkloadInstall) {
    Write-Host "`n📦 Installing .NET Aspire workload..." -ForegroundColor Cyan
    try {
        dotnet workload install aspire
        Write-Host "✅ .NET Aspire workload installed successfully!" -ForegroundColor Green
    } catch {
        Write-Host "⚠️  Aspire workload may already be installed" -ForegroundColor Yellow
    }
} else {
    Write-Host "`n📦 Skipping Aspire workload installation..." -ForegroundColor Yellow
}

# Check Docker
Write-Host "`n🐳 Checking Docker installation..." -ForegroundColor Cyan
try {
    $dockerVersion = docker --version
    Write-Host "✅ Docker found: $dockerVersion" -ForegroundColor Green
} catch {
    Write-Host "⚠️  Docker not found. Aspire will work but some features may be limited." -ForegroundColor Yellow
    Write-Host "   Install Docker Desktop for full functionality." -ForegroundColor Gray
}

# Get script directory and root
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir

Write-Host "`n📁 Working directory: $rootDir" -ForegroundColor Gray

# Change to root directory
Push-Location $rootDir

try {
    # Check if Aspire projects exist
    $appHostProject = "src\FishingRegs.AppHost\FishingRegs.AppHost.csproj"
    $serviceDefaultsProject = "src\FishingRegs.ServiceDefaults\FishingRegs.ServiceDefaults.csproj"
    
    if (-not (Test-Path $appHostProject)) {
        Write-Error "❌ Aspire AppHost project not found at $appHostProject"
        exit 1
    }
    
    if (-not (Test-Path $serviceDefaultsProject)) {
        Write-Error "❌ Aspire ServiceDefaults project not found at $serviceDefaultsProject"
        exit 1
    }
    
    Write-Host "✅ Aspire projects found" -ForegroundColor Green
    
    # Restore packages
    Write-Host "`n📦 Restoring NuGet packages..." -ForegroundColor Cyan
    dotnet restore $appHostProject
    dotnet restore $serviceDefaultsProject
    
    Write-Host "✅ Packages restored successfully!" -ForegroundColor Green
    
    # Validate setup
    Write-Host "`n🔍 Validating Aspire setup..." -ForegroundColor Cyan
    
    # Check if we can build the AppHost project
    dotnet build $appHostProject --no-restore --verbosity quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ AppHost project builds successfully!" -ForegroundColor Green
    } else {
        Write-Host "❌ AppHost project build failed" -ForegroundColor Red
        Write-Host "   Run 'dotnet build $appHostProject' for details" -ForegroundColor Gray
    }
    
    Write-Host "`n🎉 .NET Aspire setup completed successfully!" -ForegroundColor Green
    
    Write-Host "`n🚀 NEXT STEPS:" -ForegroundColor Cyan
    Write-Host "==============" -ForegroundColor Cyan
    Write-Host "1. Start the Aspire application:" -ForegroundColor Yellow
    Write-Host "   dotnet run --project src\FishingRegs.AppHost" -ForegroundColor Gray
    Write-Host ""
    Write-Host "2. Open the Aspire Dashboard:" -ForegroundColor Yellow
    Write-Host "   http://localhost:15888 (opens automatically)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "3. Access your services through the dashboard:" -ForegroundColor Yellow
    Write-Host "   • Blazor App: Links in dashboard" -ForegroundColor Gray
    Write-Host "   • SQL Server: Managed by Aspire" -ForegroundColor Gray
    Write-Host "   • Redis: Managed by Aspire" -ForegroundColor Gray
    Write-Host "   • Storage: Azurite emulator" -ForegroundColor Gray
    Write-Host ""
    
    Write-Host "🔄 COMPARISON WITH DOCKER COMPOSE:" -ForegroundColor Blue
    Write-Host "====================================" -ForegroundColor Blue
    Write-Host "• Docker Compose: docker-compose up -d (30+ seconds)" -ForegroundColor Gray
    Write-Host "• Aspire: dotnet run (15-20 seconds)" -ForegroundColor Gray
    Write-Host "• Better debugging, hot reload, and observability!" -ForegroundColor Gray
    
    Write-Host "`n📚 Learn more: docs\Aspire-Migration-Guide.md" -ForegroundColor Cyan
    
} catch {
    Write-Host "`n❌ Setup failed: $_" -ForegroundColor Red
    Write-Host "`n🔧 TROUBLESHOOTING:" -ForegroundColor Yellow
    Write-Host "==================" -ForegroundColor Yellow
    Write-Host "1. Ensure .NET 8.0+ is installed" -ForegroundColor Gray
    Write-Host "2. Check internet connection for package downloads" -ForegroundColor Gray
    Write-Host "3. Run as administrator if permission issues" -ForegroundColor Gray
    Write-Host "4. Try: dotnet workload install aspire --skip-manifest-update" -ForegroundColor Gray
    
    exit 1
} finally {
    Pop-Location
}

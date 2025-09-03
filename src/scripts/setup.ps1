#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Main entry point for setting up the Blazor Fishing Regulations development environment.

.DESCRIPTION
    This script provides a simple interface to set up the complete development environment
    including environment variables, secrets, and Docker configuration.

.PARAMETER Environment
    Target environment: Development (default), Azure

.PARAMETER Reset
    Reset all existing configuration

.EXAMPLE
    .\setup.ps1
    Sets up development environment with local services

.EXAMPLE  
    .\setup.ps1 -Environment Azure
    Sets up development environment with Azure services

.EXAMPLE
    .\setup.ps1 -Reset
    Resets and reconfigures development environment
#>

param(
    [ValidateSet("Development", "Azure")]
    [string]$Environment = "Development",
    [switch]$Reset
)

$ErrorActionPreference = "Stop"

Write-Host @"
🎣 ============================================================= 🎣
   Blazor AI Fishing Regulations - Environment Setup
🎣 ============================================================= 🎣
"@ -ForegroundColor Cyan

Write-Host "Target Environment: $Environment" -ForegroundColor Yellow
if ($Reset) {
    Write-Host "Mode: Reset and Reconfigure" -ForegroundColor Yellow
} else {
    Write-Host "Mode: Setup" -ForegroundColor Yellow
}

# Get the script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir

Write-Host "`n📁 Working directory: $rootDir" -ForegroundColor Gray

# Change to root directory
Push-Location $rootDir

try {
    Write-Host "`n🔧 Step 1: Setting up environment variables..." -ForegroundColor Cyan
    
    if ($Environment -eq "Azure") {
        & "$scriptDir\setup-dev-environment.ps1" -Azure $(if ($Reset) { "-Reset" } else { "" })
    } else {
        & "$scriptDir\setup-dev-environment.ps1" -Local $(if ($Reset) { "-Reset" } else { "" })
    }
    
    if ($LASTEXITCODE -ne 0) {
        throw "Environment setup failed"
    }
    
    Write-Host "`n🔐 Step 2: Setting up user secrets..." -ForegroundColor Cyan
    
    # Check if BlazorFishingRegs project exists
    $projectPath = "src\BlazorFishingRegs"
    if (-not (Test-Path $projectPath)) {
        Write-Host "⚠️  Blazor project not found at $projectPath" -ForegroundColor Yellow
        Write-Host "   User secrets will be configured when the project is created" -ForegroundColor Gray
    } else {
        if ($Environment -eq "Azure") {
            & "$scriptDir\setup-user-secrets.ps1" -ProjectPath $projectPath -Azure $(if ($Reset) { "-Reset" } else { "" })
        } else {
            & "$scriptDir\setup-user-secrets.ps1" -ProjectPath $projectPath $(if ($Reset) { "-Reset" } else { "" })
        }
    }
    
    Write-Host "`n✅ Step 3: Validating configuration..." -ForegroundColor Cyan
    
    & "$scriptDir\validate-environment.ps1" -Environment Development
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "`n🎉 SUCCESS! Environment setup completed successfully!" -ForegroundColor Green
        
        Write-Host "`n🚀 NEXT STEPS:" -ForegroundColor Cyan
        Write-Host "==============" -ForegroundColor Cyan
        Write-Host "1. Start Docker services:" -ForegroundColor Yellow
        Write-Host "   docker-compose up -d" -ForegroundColor Gray
        Write-Host ""
        Write-Host "2. Check service status:" -ForegroundColor Yellow
        Write-Host "   docker-compose ps" -ForegroundColor Gray
        Write-Host ""
        Write-Host "3. View logs:" -ForegroundColor Yellow
        Write-Host "   docker-compose logs -f" -ForegroundColor Gray
        Write-Host ""
        Write-Host "4. Access services:" -ForegroundColor Yellow
        Write-Host "   • Blazor App: https://localhost:8443" -ForegroundColor Gray
        Write-Host "   • Seq Logs: http://localhost:8081" -ForegroundColor Gray
        Write-Host "   • SQL Server: localhost:1433" -ForegroundColor Gray
        Write-Host ""
        
        if ($Environment -eq "Development") {
            Write-Host "📝 NOTE: Using local mock services for development" -ForegroundColor Blue
        } else {
            Write-Host "📝 NOTE: Using live Azure services" -ForegroundColor Blue
        }
        
    } else {
        Write-Host "`n⚠️  Environment validation found issues" -ForegroundColor Yellow
        Write-Host "   Please review the validation report and fix any issues" -ForegroundColor Gray
        Write-Host "   Run validation again: .\src\scripts\validate-environment.ps1 -Environment Development" -ForegroundColor Gray
    }
    
} catch {
    Write-Host "`n❌ Setup failed: $_" -ForegroundColor Red
    Write-Host "`n🔧 TROUBLESHOOTING:" -ForegroundColor Yellow
    Write-Host "==================" -ForegroundColor Yellow
    Write-Host "1. Check the detailed error message above" -ForegroundColor Gray
    Write-Host "2. Ensure you have required permissions" -ForegroundColor Gray
    Write-Host "3. Verify Azure CLI is installed (for Azure environment)" -ForegroundColor Gray
    Write-Host "4. Check network connectivity" -ForegroundColor Gray
    Write-Host "5. Review setup logs for more details" -ForegroundColor Gray
    
    exit 1
} finally {
    Pop-Location
}

Write-Host "`n📚 For more information, see: src\config\README.md" -ForegroundColor Cyan

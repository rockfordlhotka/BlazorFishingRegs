#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Sets up development environment variables and secrets for the Blazor Fishing Regulations application.

.DESCRIPTION
    This script configures all necessary environment variables and secrets for local development,
    including Azure services, database connections, and application settings.

.PARAMETER Azure
    Switch to set up Azure service environment variables

.PARAMETER Local
    Switch to set up local development environment variables (default)

.PARAMETER Reset
    Switch to clear all existing environment variables

.EXAMPLE
    .\setup-dev-environment.ps1 -Azure
    Sets up environment for Azure services

.EXAMPLE
    .\setup-dev-environment.ps1 -Local
    Sets up environment for local development
#>

param(
    [switch]$Azure,
    [switch]$Local = $true,
    [switch]$Reset,
    [string]$ConfigPath = "src/config/environments"
)

# Set error action preference
$ErrorActionPreference = "Stop"

Write-Host "🎣 Blazor Fishing Regulations - Development Environment Setup" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# Function to set environment variable
function Set-EnvVar {
    param(
        [string]$Name,
        [string]$Value,
        [string]$Description = ""
    )
    
    if ($Description) {
        Write-Host "Setting $Name - $Description" -ForegroundColor Green
    } else {
        Write-Host "Setting $Name" -ForegroundColor Green
    }
    
    [Environment]::SetEnvironmentVariable($Name, $Value, "User")
    $env:$Name = $Value
}

# Function to prompt for secret value
function Get-SecretValue {
    param(
        [string]$Name,
        [string]$Description,
        [bool]$Required = $true
    )
    
    Write-Host "`n📝 $Description" -ForegroundColor Yellow
    $value = Read-Host "Enter $Name"
    
    if ($Required -and [string]::IsNullOrWhiteSpace($value)) {
        Write-Error "❌ $Name is required!"
        exit 1
    }
    
    return $value
}

# Reset environment variables if requested
if ($Reset) {
    Write-Host "`n🧹 Clearing existing environment variables..." -ForegroundColor Yellow
    
    $varsToRemove = @(
        "BLAZOR_FISHING_ENV",
        "AZURE_OPENAI_ENDPOINT",
        "AZURE_OPENAI_API_KEY", 
        "AZURE_OPENAI_DEPLOYMENT_NAME",
        "AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT",
        "AZURE_DOCUMENT_INTELLIGENCE_API_KEY",
        "AZURE_STORAGE_CONNECTION_STRING",
        "AZURE_STORAGE_CONTAINER_NAME",
        "SQL_CONNECTION_STRING",
        "REDIS_CONNECTION_STRING",
        "SEQ_SERVER_URL",
        "SEQ_API_KEY",
        "ASPNETCORE_ENVIRONMENT",
        "FISHING_REGS_API_KEY",
        "FISHING_REGS_JWT_SECRET"
    )
    
    foreach ($var in $varsToRemove) {
        [Environment]::SetEnvironmentVariable($var, $null, "User")
        Write-Host "Removed $var" -ForegroundColor Gray
    }
    
    Write-Host "✅ Environment variables cleared!" -ForegroundColor Green
    if (-not $Azure -and -not $Local) {
        exit 0
    }
}

# Common environment variables
Write-Host "`n🔧 Setting up common environment variables..." -ForegroundColor Cyan

Set-EnvVar "ASPNETCORE_ENVIRONMENT" "Development" "ASP.NET Core environment"
Set-EnvVar "BLAZOR_FISHING_ENV" "Development" "Application environment identifier"

# Local development setup
if ($Local -or (-not $Azure)) {
    Write-Host "`n🏠 Setting up LOCAL development environment..." -ForegroundColor Cyan
    
    # Database settings
    Set-EnvVar "SQL_CONNECTION_STRING" "Server=localhost,1433;Database=FishingRegsDB;User Id=sa;Password=FishingRegs2025!;TrustServerCertificate=True;MultipleActiveResultSets=True" "SQL Server connection"
    
    # Redis settings
    Set-EnvVar "REDIS_CONNECTION_STRING" "localhost:6379" "Redis cache connection"
    
    # Azurite (local Azure Storage emulator)
    Set-EnvVar "AZURE_STORAGE_CONNECTION_STRING" "UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://localhost" "Local storage emulator"
    Set-EnvVar "AZURE_STORAGE_CONTAINER_NAME" "fishing-regulations" "Storage container for PDFs"
    
    # Seq logging
    Set-EnvVar "SEQ_SERVER_URL" "http://localhost:5341" "Seq logging server"
    Set-EnvVar "SEQ_API_KEY" "" "Seq API key (optional for development)"
    
    # Application secrets
    Set-EnvVar "FISHING_REGS_API_KEY" "dev-api-key-$(Get-Random)" "API key for development"
    Set-EnvVar "FISHING_REGS_JWT_SECRET" "dev-jwt-secret-$(Get-Random)-$(Get-Date -Format 'yyyyMMdd')" "JWT signing secret"
    
    # Mock AI services for local development
    Set-EnvVar "USE_MOCK_AI_SERVICES" "true" "Use mock AI services instead of Azure"
    Set-EnvVar "MOCK_AI_SERVICE_URL" "http://localhost:7000" "Mock AI service endpoint"
    
    Write-Host "`n✅ Local development environment configured!" -ForegroundColor Green
    Write-Host "📝 Note: Using local services (Docker containers)" -ForegroundColor Yellow
}

# Azure services setup
if ($Azure) {
    Write-Host "`n☁️ Setting up AZURE services environment..." -ForegroundColor Cyan
    
    # Azure OpenAI settings
    Write-Host "`n🤖 Azure OpenAI Configuration" -ForegroundColor Magenta
    $openaiEndpoint = Get-SecretValue "AZURE_OPENAI_ENDPOINT" "Azure OpenAI endpoint (e.g., https://your-openai.openai.azure.com/)"
    $openaiKey = Get-SecretValue "AZURE_OPENAI_API_KEY" "Azure OpenAI API key"
    $openaiDeployment = Get-SecretValue "AZURE_OPENAI_DEPLOYMENT_NAME" "Azure OpenAI GPT-4 deployment name (e.g., gpt-4)"
    
    Set-EnvVar "AZURE_OPENAI_ENDPOINT" $openaiEndpoint
    Set-EnvVar "AZURE_OPENAI_API_KEY" $openaiKey  
    Set-EnvVar "AZURE_OPENAI_DEPLOYMENT_NAME" $openaiDeployment
    
    # Azure Document Intelligence settings
    Write-Host "`n📄 Azure Document Intelligence Configuration" -ForegroundColor Magenta
    $docIntelEndpoint = Get-SecretValue "AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT" "Azure Document Intelligence endpoint"
    $docIntelKey = Get-SecretValue "AZURE_DOCUMENT_INTELLIGENCE_API_KEY" "Azure Document Intelligence API key"
    
    Set-EnvVar "AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT" $docIntelEndpoint
    Set-EnvVar "AZURE_DOCUMENT_INTELLIGENCE_API_KEY" $docIntelKey
    
    # Azure Storage settings
    Write-Host "`n💾 Azure Storage Configuration" -ForegroundColor Magenta
    $storageConnectionString = Get-SecretValue "AZURE_STORAGE_CONNECTION_STRING" "Azure Storage connection string"
    $storageContainer = Read-Host "Enter storage container name (default: fishing-regulations)"
    if ([string]::IsNullOrWhiteSpace($storageContainer)) {
        $storageContainer = "fishing-regulations"
    }
    
    Set-EnvVar "AZURE_STORAGE_CONNECTION_STRING" $storageConnectionString
    Set-EnvVar "AZURE_STORAGE_CONTAINER_NAME" $storageContainer
    
    # Disable mock services
    Set-EnvVar "USE_MOCK_AI_SERVICES" "false" "Disable mock AI services"
    
    Write-Host "`n✅ Azure services environment configured!" -ForegroundColor Green
    Write-Host "📝 Note: Using live Azure services" -ForegroundColor Yellow
}

# Configure Aspire User Secrets
Write-Host "`n� Configuring Aspire user secrets..." -ForegroundColor Cyan

# Set up user secrets for Aspire AppHost project
$appHostProject = "src\FishingRegs.AppHost"
if (Test-Path $appHostProject) {
    Push-Location $appHostProject
    try {
        dotnet user-secrets init --force 2>$null
        
        if ($Azure) {
            # Azure services configuration
            dotnet user-secrets set "ConnectionStrings:AzureOpenAI" $env:AZURE_OPENAI_ENDPOINT
            dotnet user-secrets set "AzureOpenAI:ApiKey" $env:AZURE_OPENAI_API_KEY
            dotnet user-secrets set "AzureOpenAI:DeploymentName" $env:AZURE_OPENAI_DEPLOYMENT_NAME
            dotnet user-secrets set "AzureDocumentIntelligence:Endpoint" $env:AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT
            dotnet user-secrets set "AzureDocumentIntelligence:ApiKey" $env:AZURE_DOCUMENT_INTELLIGENCE_API_KEY
            dotnet user-secrets set "ConnectionStrings:AzureStorage" $env:AZURE_STORAGE_CONNECTION_STRING
            dotnet user-secrets set "ApplicationSettings:UseMockAIServices" "false"
        } else {
            # Local development configuration
            dotnet user-secrets set "ApplicationSettings:UseMockAIServices" "true"
        }
        
        Write-Host "✅ Aspire user secrets configured" -ForegroundColor Green
    } finally {
        Pop-Location
    }
} else {
    Write-Host "⚠️  Aspire AppHost project not found - secrets will be configured when project is created" -ForegroundColor Yellow
}

# Display summary
Write-Host "`n📋 SETUP SUMMARY" -ForegroundColor Cyan
Write-Host "=================" -ForegroundColor Cyan
Write-Host "Environment: $($env:BLAZOR_FISHING_ENV)" -ForegroundColor White
Write-Host "Storage: $(if ($Azure) { 'Azure Storage' } else { 'Azurite (Local)' })" -ForegroundColor White
Write-Host "AI Services: $(if ($Azure) { 'Azure OpenAI + Document Intelligence' } else { 'Mock Services' })" -ForegroundColor White
Write-Host "Orchestration: .NET Aspire" -ForegroundColor White

Write-Host "`n🚀 NEXT STEPS" -ForegroundColor Green
Write-Host "=============" -ForegroundColor Green
Write-Host "1. Start Aspire application: dotnet run --project src\FishingRegs.AppHost" -ForegroundColor Yellow
Write-Host "2. Open Aspire Dashboard: http://localhost:15888 (opens automatically)" -ForegroundColor Yellow
Write-Host "3. Access services through the dashboard" -ForegroundColor Yellow

if ($Azure) {
    Write-Host "`n⚠️  SECURITY NOTICE" -ForegroundColor Red
    Write-Host "==================" -ForegroundColor Red
    Write-Host "Azure secrets are stored in user environment variables." -ForegroundColor Yellow
    Write-Host "Consider using Azure Key Vault for production." -ForegroundColor Yellow
}

Write-Host "`n✅ Environment setup complete!" -ForegroundColor Green

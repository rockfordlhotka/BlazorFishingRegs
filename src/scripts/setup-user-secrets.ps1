#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Configures .NET User Secrets for the Blazor Fishing Regulations application.

.DESCRIPTION
    This script sets up user secrets for local development without storing sensitive data in source control.
    It initializes the user secrets store and sets common development secrets.

.PARAMETER ProjectPath
    Path to the project file (.csproj) - defaults to src/BlazorFishingRegs

.PARAMETER Reset
    Clears all existing user secrets before setting new ones

.PARAMETER Azure
    Sets up secrets for Azure services instead of local development

.EXAMPLE
    .\setup-user-secrets.ps1
    Sets up basic development secrets

.EXAMPLE
    .\setup-user-secrets.ps1 -Azure
    Sets up secrets for Azure services

.EXAMPLE
    .\setup-user-secrets.ps1 -Reset
    Clears existing secrets and sets up new ones
#>

param(
    [string]$ProjectPath = "src/BlazorFishingRegs",
    [switch]$Reset,
    [switch]$Azure
)

$ErrorActionPreference = "Stop"

Write-Host "🔐 Blazor Fishing Regulations - User Secrets Setup" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

# Check if project exists
$projectFile = Get-ChildItem -Path $ProjectPath -Filter "*.csproj" -ErrorAction SilentlyContinue | Select-Object -First 1

if (-not $projectFile) {
    Write-Error "❌ No .csproj file found in $ProjectPath"
    exit 1
}

$projectFilePath = $projectFile.FullName
Write-Host "📁 Project file: $projectFilePath" -ForegroundColor Green

# Initialize user secrets if not already done
Write-Host "`n🔧 Initializing user secrets..." -ForegroundColor Cyan
Push-Location (Split-Path $projectFilePath)

try {
    dotnet user-secrets init
    Write-Host "✅ User secrets initialized" -ForegroundColor Green
} catch {
    Write-Host "ℹ️ User secrets already initialized" -ForegroundColor Yellow
}

# Clear existing secrets if requested
if ($Reset) {
    Write-Host "`n🧹 Clearing existing user secrets..." -ForegroundColor Yellow
    dotnet user-secrets clear
    Write-Host "✅ User secrets cleared" -ForegroundColor Green
}

# Function to set user secret
function Set-UserSecret {
    param(
        [string]$Key,
        [string]$Value,
        [string]$Description = ""
    )
    
    if ($Description) {
        Write-Host "Setting $Key - $Description" -ForegroundColor Green
    } else {
        Write-Host "Setting $Key" -ForegroundColor Green
    }
    
    dotnet user-secrets set $Key $Value
}

# Function to prompt for secret value
function Get-SecretInput {
    param(
        [string]$Name,
        [string]$Description,
        [string]$Default = "",
        [bool]$Required = $true
    )
    
    Write-Host "`n📝 $Description" -ForegroundColor Yellow
    if ($Default) {
        $prompt = "Enter $Name (default: $Default)"
    } else {
        $prompt = "Enter $Name"
    }
    
    $value = Read-Host $prompt
    
    if ([string]::IsNullOrWhiteSpace($value) -and $Default) {
        $value = $Default
    }
    
    if ($Required -and [string]::IsNullOrWhiteSpace($value)) {
        Write-Error "❌ $Name is required!"
        exit 1
    }
    
    return $value
}

# Set common development secrets
Write-Host "`n🏠 Setting up development secrets..." -ForegroundColor Cyan

if (-not $Azure) {
    # Local development configuration
    Set-UserSecret "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=FishingRegsDB;User Id=sa;Password=FishingRegs2025!;TrustServerCertificate=True;MultipleActiveResultSets=True" "SQL Server connection"
    Set-UserSecret "ConnectionStrings:Redis" "localhost:6379" "Redis cache connection"
    Set-UserSecret "ConnectionStrings:AzureStorage" "UseDevelopmentStorage=true" "Local storage emulator"
    
    Set-UserSecret "ApplicationSettings:UseMockAIServices" "true" "Use mock AI services"
    Set-UserSecret "ApplicationSettings:MockAIServiceUrl" "http://localhost:7000" "Mock AI service URL"
    Set-UserSecret "ApplicationSettings:ApiKey" "dev-api-key-$(Get-Random)" "Development API key"
    Set-UserSecret "ApplicationSettings:JwtSecret" "dev-jwt-secret-$(Get-Random)-$(Get-Date -Format 'yyyyMMdd')" "Development JWT secret"
    
    Set-UserSecret "Seq:ServerUrl" "http://localhost:5341" "Seq logging URL"
    Set-UserSecret "Seq:ApiKey" "" "Seq API key (empty for development)"
    
    Write-Host "`n✅ Local development secrets configured!" -ForegroundColor Green
} else {
    # Azure services configuration
    Write-Host "`n☁️ Setting up Azure service secrets..." -ForegroundColor Cyan
    
    # Azure OpenAI
    Write-Host "`n🤖 Azure OpenAI Configuration" -ForegroundColor Magenta
    $openaiEndpoint = Get-SecretInput "Azure OpenAI Endpoint" "Azure OpenAI service endpoint (e.g., https://your-openai.openai.azure.com/)"
    $openaiKey = Get-SecretInput "Azure OpenAI API Key" "Azure OpenAI API key"
    $openaiDeployment = Get-SecretInput "Azure OpenAI Deployment" "GPT-4 deployment name" "gpt-4"
    
    Set-UserSecret "AzureOpenAI:Endpoint" $openaiEndpoint
    Set-UserSecret "AzureOpenAI:ApiKey" $openaiKey
    Set-UserSecret "AzureOpenAI:DeploymentName" $openaiDeployment
    
    # Azure Document Intelligence
    Write-Host "`n📄 Azure Document Intelligence Configuration" -ForegroundColor Magenta
    $docIntelEndpoint = Get-SecretInput "Document Intelligence Endpoint" "Azure Document Intelligence endpoint"
    $docIntelKey = Get-SecretInput "Document Intelligence API Key" "Azure Document Intelligence API key"
    
    Set-UserSecret "AzureDocumentIntelligence:Endpoint" $docIntelEndpoint
    Set-UserSecret "AzureDocumentIntelligence:ApiKey" $docIntelKey
    
    # Azure Storage
    Write-Host "`n💾 Azure Storage Configuration" -ForegroundColor Magenta
    $storageConnection = Get-SecretInput "Azure Storage Connection String" "Azure Storage connection string"
    $storageContainer = Get-SecretInput "Storage Container Name" "Storage container for PDFs" "fishing-regulations"
    
    Set-UserSecret "AzureStorage:ConnectionString" $storageConnection
    Set-UserSecret "AzureStorage:ContainerName" $storageContainer
    Set-UserSecret "ConnectionStrings:AzureStorage" $storageConnection
    
    # Application settings for Azure
    Set-UserSecret "ApplicationSettings:UseMockAIServices" "false" "Disable mock services"
    $apiKey = Get-SecretInput "Application API Key" "Production API key for external access" "" $false
    if ($apiKey) {
        Set-UserSecret "ApplicationSettings:ApiKey" $apiKey
    }
    
    $jwtSecret = Get-SecretInput "JWT Secret" "JWT signing secret (minimum 256 bits)" "" $false
    if ($jwtSecret) {
        Set-UserSecret "ApplicationSettings:JwtSecret" $jwtSecret
    }
    
    Write-Host "`n✅ Azure service secrets configured!" -ForegroundColor Green
}

# Set additional common secrets
Write-Host "`n⚙️ Setting additional configuration..." -ForegroundColor Cyan

Set-UserSecret "AllowedHosts" "*" "Allowed hosts for development"
Set-UserSecret "CorsSettings:AllowedOrigins:0" "https://localhost:8443" "CORS origin 1"
Set-UserSecret "CorsSettings:AllowedOrigins:1" "http://localhost:8080" "CORS origin 2"
Set-UserSecret "RateLimit:RequestsPerMinute" "1000" "Rate limit for development"
Set-UserSecret "RateLimit:EnableRateLimit" "false" "Disable rate limiting for development"
Set-UserSecret "FileUpload:MaxFileSizeMB" "100" "Max PDF file size"
Set-UserSecret "FileUpload:MaxConcurrentUploads" "10" "Max concurrent uploads"

# List configured secrets
Write-Host "`n📋 Configured User Secrets:" -ForegroundColor Cyan
dotnet user-secrets list

Write-Host "`n✅ User secrets setup complete!" -ForegroundColor Green

Write-Host "`n📝 NOTES:" -ForegroundColor Yellow
Write-Host "- User secrets are stored locally and not committed to source control" -ForegroundColor Gray
Write-Host "- Secrets override values in appsettings.json" -ForegroundColor Gray
Write-Host "- To view secrets: dotnet user-secrets list" -ForegroundColor Gray
Write-Host "- To remove a secret: dotnet user-secrets remove <key>" -ForegroundColor Gray
Write-Host "- To clear all secrets: dotnet user-secrets clear" -ForegroundColor Gray

Pop-Location

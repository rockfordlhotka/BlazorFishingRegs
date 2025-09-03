#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Sets up Azure Key Vault secrets for the Blazor Fishing Regulations application production deployment.

.DESCRIPTION
    This script creates and configures Azure Key Vault secrets for production use.
    It can create the Key Vault, set up access policies, and store all required secrets.

.PARAMETER KeyVaultName
    Name of the Azure Key Vault (required)

.PARAMETER ResourceGroupName
    Name of the Azure Resource Group containing the Key Vault

.PARAMETER SubscriptionId
    Azure subscription ID (optional - uses current subscription if not specified)

.PARAMETER Location
    Azure region for Key Vault creation (default: East US)

.PARAMETER CreateKeyVault
    Switch to create a new Key Vault

.PARAMETER Interactive
    Switch to prompt for all secret values interactively

.EXAMPLE
    .\setup-azure-keyvault.ps1 -KeyVaultName "fishing-regs-kv" -ResourceGroupName "fishing-regs-rg" -CreateKeyVault
    Creates a new Key Vault and sets up secrets

.EXAMPLE
    .\setup-azure-keyvault.ps1 -KeyVaultName "fishing-regs-kv" -Interactive
    Sets up secrets in existing Key Vault with interactive prompts
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$KeyVaultName,
    
    [string]$ResourceGroupName = "fishing-regs-rg",
    [string]$SubscriptionId = "",
    [string]$Location = "East US",
    [switch]$CreateKeyVault,
    [switch]$Interactive
)

$ErrorActionPreference = "Stop"

Write-Host "🔐 Azure Key Vault Setup for Blazor Fishing Regulations" -ForegroundColor Cyan
Write-Host "=======================================================" -ForegroundColor Cyan

# Check if Azure CLI is installed
if (-not (Get-Command "az" -ErrorAction SilentlyContinue)) {
    Write-Error "❌ Azure CLI is not installed. Please install it from https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
}

# Login to Azure if not already logged in
Write-Host "`n🔐 Checking Azure authentication..." -ForegroundColor Cyan
$loginCheck = az account show 2>$null
if (-not $loginCheck) {
    Write-Host "Please log in to Azure..." -ForegroundColor Yellow
    az login
}

# Set subscription if provided
if ($SubscriptionId) {
    Write-Host "Setting subscription to $SubscriptionId..." -ForegroundColor Green
    az account set --subscription $SubscriptionId
}

$currentSubscription = az account show --query "name" -o tsv
Write-Host "✅ Using subscription: $currentSubscription" -ForegroundColor Green

# Create Key Vault if requested
if ($CreateKeyVault) {
    Write-Host "`n🏗️ Creating Azure Key Vault..." -ForegroundColor Cyan
    
    # Check if resource group exists
    $rgExists = az group exists --name $ResourceGroupName
    if ($rgExists -eq "false") {
        Write-Host "Creating resource group $ResourceGroupName..." -ForegroundColor Yellow
        az group create --name $ResourceGroupName --location $Location
    }
    
    # Create Key Vault
    Write-Host "Creating Key Vault $KeyVaultName..." -ForegroundColor Yellow
    az keyvault create `
        --name $KeyVaultName `
        --resource-group $ResourceGroupName `
        --location $Location `
        --sku standard `
        --enable-rbac-authorization false
    
    Write-Host "✅ Key Vault created successfully!" -ForegroundColor Green
}

# Function to set Key Vault secret
function Set-KeyVaultSecret {
    param(
        [string]$SecretName,
        [string]$SecretValue,
        [string]$Description = ""
    )
    
    if ($Description) {
        Write-Host "Setting secret: $SecretName - $Description" -ForegroundColor Green
    } else {
        Write-Host "Setting secret: $SecretName" -ForegroundColor Green
    }
    
    try {
        az keyvault secret set `
            --vault-name $KeyVaultName `
            --name $SecretName `
            --value $SecretValue `
            --output none
    } catch {
        Write-Error "❌ Failed to set secret $SecretName : $_"
    }
}

# Function to prompt for secret value
function Get-SecretInput {
    param(
        [string]$SecretName,
        [string]$Description,
        [string]$Example = "",
        [bool]$Required = $true
    )
    
    Write-Host "`n📝 $Description" -ForegroundColor Yellow
    if ($Example) {
        Write-Host "Example: $Example" -ForegroundColor Gray
    }
    
    $value = Read-Host "Enter value for $SecretName"
    
    if ($Required -and [string]::IsNullOrWhiteSpace($value)) {
        Write-Error "❌ $SecretName is required!"
        exit 1
    }
    
    return $value
}

# Set up secrets
Write-Host "`n🔧 Setting up Key Vault secrets..." -ForegroundColor Cyan

if ($Interactive) {
    # Interactive mode - prompt for all values
    Write-Host "`n🤖 Azure OpenAI Configuration" -ForegroundColor Magenta
    $openaiEndpoint = Get-SecretInput "azure-openai-endpoint" "Azure OpenAI service endpoint" "https://your-openai.openai.azure.com/"
    $openaiKey = Get-SecretInput "azure-openai-api-key" "Azure OpenAI API key"
    $openaiDeployment = Get-SecretInput "azure-openai-deployment-name" "GPT-4 deployment name" "gpt-4"
    
    Set-KeyVaultSecret "azure-openai-endpoint" $openaiEndpoint "Azure OpenAI endpoint"
    Set-KeyVaultSecret "azure-openai-api-key" $openaiKey "Azure OpenAI API key"
    Set-KeyVaultSecret "azure-openai-deployment-name" $openaiDeployment "Azure OpenAI deployment"
    
    Write-Host "`n📄 Azure Document Intelligence Configuration" -ForegroundColor Magenta
    $docIntelEndpoint = Get-SecretInput "azure-document-intelligence-endpoint" "Azure Document Intelligence endpoint"
    $docIntelKey = Get-SecretInput "azure-document-intelligence-api-key" "Azure Document Intelligence API key"
    
    Set-KeyVaultSecret "azure-document-intelligence-endpoint" $docIntelEndpoint "Document Intelligence endpoint"
    Set-KeyVaultSecret "azure-document-intelligence-api-key" $docIntelKey "Document Intelligence API key"
    
    Write-Host "`n🗃️ Database Configuration" -ForegroundColor Magenta
    $sqlConnection = Get-SecretInput "sql-connection-string" "SQL Server connection string" "Server=your-server.database.windows.net;Database=FishingRegsDB;Authentication=Active Directory Default"
    Set-KeyVaultSecret "sql-connection-string" $sqlConnection "SQL Server connection"
    
    Write-Host "`n🔄 Redis Configuration" -ForegroundColor Magenta
    $redisConnection = Get-SecretInput "redis-connection-string" "Redis connection string" "your-cache.redis.cache.windows.net:6380,password=key,ssl=True"
    Set-KeyVaultSecret "redis-connection-string" $redisConnection "Redis connection"
    
    Write-Host "`n💾 Storage Configuration" -ForegroundColor Magenta
    $storageConnection = Get-SecretInput "azure-storage-connection-string" "Azure Storage connection string"
    Set-KeyVaultSecret "azure-storage-connection-string" $storageConnection "Azure Storage connection"
    
    Write-Host "`n🔑 Application Secrets" -ForegroundColor Magenta
    $apiKey = Get-SecretInput "fishing-regs-api-key" "Application API key for external access"
    $jwtSecret = Get-SecretInput "fishing-regs-jwt-secret" "JWT signing secret (minimum 256 bits)"
    
    Set-KeyVaultSecret "fishing-regs-api-key" $apiKey "Application API key"
    Set-KeyVaultSecret "fishing-regs-jwt-secret" $jwtSecret "JWT signing secret"
    
    Write-Host "`n📊 Monitoring Configuration" -ForegroundColor Magenta
    $seqApiKey = Get-SecretInput "seq-api-key" "Seq logging API key" "" $false
    if (-not [string]::IsNullOrWhiteSpace($seqApiKey)) {
        Set-KeyVaultSecret "seq-api-key" $seqApiKey "Seq API key"
    }
    
    $appInsightsConnection = Get-SecretInput "application-insights-connection-string" "Application Insights connection string" "" $false
    if (-not [string]::IsNullOrWhiteSpace($appInsightsConnection)) {
        Set-KeyVaultSecret "application-insights-connection-string" $appInsightsConnection "Application Insights connection"
    }
    
} else {
    # Template mode - set placeholder values that need to be updated
    Write-Host "`n📝 Setting template secrets (update these with real values)..." -ForegroundColor Yellow
    
    # Azure OpenAI
    Set-KeyVaultSecret "azure-openai-endpoint" "https://your-openai-resource.openai.azure.com/" "Azure OpenAI endpoint (TEMPLATE)"
    Set-KeyVaultSecret "azure-openai-api-key" "your-azure-openai-api-key-here" "Azure OpenAI API key (TEMPLATE)"
    Set-KeyVaultSecret "azure-openai-deployment-name" "gpt-4" "Azure OpenAI deployment name"
    
    # Azure Document Intelligence
    Set-KeyVaultSecret "azure-document-intelligence-endpoint" "https://your-doc-intel.cognitiveservices.azure.com/" "Document Intelligence endpoint (TEMPLATE)"
    Set-KeyVaultSecret "azure-document-intelligence-api-key" "your-document-intelligence-api-key-here" "Document Intelligence API key (TEMPLATE)"
    
    # Database
    Set-KeyVaultSecret "sql-connection-string" "Server=your-server.database.windows.net;Database=FishingRegsDB;Authentication=Active Directory Default;TrustServerCertificate=True" "SQL connection (TEMPLATE)"
    
    # Redis
    Set-KeyVaultSecret "redis-connection-string" "your-cache.redis.cache.windows.net:6380,password=your-key,ssl=True,abortConnect=False" "Redis connection (TEMPLATE)"
    
    # Storage
    Set-KeyVaultSecret "azure-storage-connection-string" "DefaultEndpointsProtocol=https;AccountName=your-account;AccountKey=your-key;EndpointSuffix=core.windows.net" "Storage connection (TEMPLATE)"
    
    # Application secrets
    $randomApiKey = "fishing-regs-api-$(Get-Random)-$(Get-Date -Format 'yyyyMMdd')"
    $randomJwtSecret = "jwt-secret-$(Get-Random)-$(Get-Random)-$(Get-Date -Format 'yyyyMMddHHmmss')"
    
    Set-KeyVaultSecret "fishing-regs-api-key" $randomApiKey "Application API key"
    Set-KeyVaultSecret "fishing-regs-jwt-secret" $randomJwtSecret "JWT signing secret"
    
    # SSL certificate password
    Set-KeyVaultSecret "ssl-certificate-password" "your-ssl-certificate-password-here" "SSL certificate password (TEMPLATE)"
    
    Write-Host "`n⚠️  IMPORTANT: Update template values with real secrets!" -ForegroundColor Red
}

# Set up access policies
Write-Host "`n🔐 Setting up Key Vault access policies..." -ForegroundColor Cyan

# Get current user
$currentUser = az ad signed-in-user show --query "id" -o tsv

# Grant current user full access to secrets
Write-Host "Granting access to current user..." -ForegroundColor Green
az keyvault set-policy `
    --name $KeyVaultName `
    --object-id $currentUser `
    --secret-permissions get list set delete recover backup restore

Write-Host "`n✅ Key Vault setup complete!" -ForegroundColor Green

# Display summary
Write-Host "`n📋 SETUP SUMMARY" -ForegroundColor Cyan
Write-Host "================" -ForegroundColor Cyan
Write-Host "Key Vault Name: $KeyVaultName" -ForegroundColor White
Write-Host "Resource Group: $ResourceGroupName" -ForegroundColor White
Write-Host "Subscription: $currentSubscription" -ForegroundColor White

# List created secrets
Write-Host "`n📝 Created Secrets:" -ForegroundColor Cyan
az keyvault secret list --vault-name $KeyVaultName --query "[].name" -o table

Write-Host "`n🚀 NEXT STEPS" -ForegroundColor Green
Write-Host "=============" -ForegroundColor Green
Write-Host "1. Update your application configuration to use Key Vault" -ForegroundColor Yellow
Write-Host "2. Configure Managed Identity for your app service" -ForegroundColor Yellow
Write-Host "3. Update Key Vault access policies for your application" -ForegroundColor Yellow

if (-not $Interactive) {
    Write-Host "4. Replace template values with real secrets:" -ForegroundColor Yellow
    Write-Host "   az keyvault secret set --vault-name $KeyVaultName --name <secret-name> --value <real-value>" -ForegroundColor Gray
}

Write-Host "`n📖 Key Vault URI: https://$KeyVaultName.vault.azure.net/" -ForegroundColor Cyan

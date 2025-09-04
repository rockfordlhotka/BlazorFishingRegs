# Secure Configuration Guide

This guide explains how to securely configure the Fishing Regulations application with Azure credentials for both development and production environments.

## Overview

The application uses a secure configuration system that:
- Uses **User Secrets** for development (never committed to source control)
- Uses **Azure Key Vault** for production (secured with Azure identity)
- Automatically detects environment and chooses appropriate credential source
- Provides fallback to environment variables if needed

## Required Configuration

The following secrets must be configured:

| Setting | Purpose | Example Value |
|---------|---------|---------------|
| `AzureAI:DocumentIntelligence:Endpoint` | Azure AI Document Intelligence service endpoint | `https://your-doc-intelligence.cognitiveservices.azure.com/` |
| `AzureAI:DocumentIntelligence:ApiKey` | API key for Document Intelligence | `your-32-character-api-key` |
| `ConnectionStrings:AzureStorage` | Azure Blob Storage connection string | `DefaultEndpointsProtocol=https;AccountName=...` |

## Development Setup (User Secrets)

### 1. Initialize User Secrets (if not already done)

```powershell
# Navigate to your project directory
cd "src\FishingRegs.TestConsole"  # or BlazorFishingRegs for main app

# Initialize user secrets
dotnet user-secrets init
```

### 2. Set Required Secrets

```powershell
# Set Document Intelligence endpoint
dotnet user-secrets set "AzureAI:DocumentIntelligence:Endpoint" "https://your-doc-intelligence.cognitiveservices.azure.com/"

# Set Document Intelligence API key
dotnet user-secrets set "AzureAI:DocumentIntelligence:ApiKey" "your-api-key-here"

# Set Azure Storage connection string
dotnet user-secrets set "ConnectionStrings:AzureStorage" "DefaultEndpointsProtocol=https;AccountName=yourstorageaccount;AccountKey=your-key;EndpointSuffix=core.windows.net"
```

### 3. Verify Configuration

```powershell
# List all user secrets
dotnet user-secrets list

# Test the configuration
dotnet run
```

## Production Setup (Azure Key Vault)

### 1. Create Azure Key Vault

```bash
# Create resource group (if needed)
az group create --name "rg-fishing-regs" --location "East US"

# Create Key Vault
az keyvault create --name "kv-fishing-regs-prod" --resource-group "rg-fishing-regs" --location "East US"
```

### 2. Add Secrets to Key Vault

**Important:** Key Vault secret names use `--` instead of `:` for nested configuration.

```bash
# Set Document Intelligence endpoint
az keyvault secret set --vault-name "kv-fishing-regs-prod" --name "AzureAI--DocumentIntelligence--Endpoint" --value "https://your-doc-intelligence.cognitiveservices.azure.com/"

# Set Document Intelligence API key
az keyvault secret set --vault-name "kv-fishing-regs-prod" --name "AzureAI--DocumentIntelligence--ApiKey" --value "your-api-key-here"

# Set Azure Storage connection string
az keyvault secret set --vault-name "kv-fishing-regs-prod" --name "ConnectionStrings--AzureStorage" --value "your-storage-connection-string"
```

### 3. Configure Application Identity

The application uses `DefaultAzureCredential` which supports multiple authentication methods:

#### Option A: Managed Identity (Recommended for Azure hosting)

```bash
# Enable system-assigned managed identity for your App Service
az webapp identity assign --name "your-app-name" --resource-group "rg-fishing-regs"

# Grant access to Key Vault
az keyvault set-policy --name "kv-fishing-regs-prod" --object-id "your-managed-identity-id" --secret-permissions get list
```

#### Option B: Service Principal (for other hosting)

```bash
# Create service principal
az ad sp create-for-rbac --name "fishing-regs-app" --skip-assignment

# Grant access to Key Vault
az keyvault set-policy --name "kv-fishing-regs-prod" --spn "your-service-principal-id" --secret-permissions get list
```

### 4. Set Environment Variable

Set the Key Vault URI in your production environment:

```bash
# For App Service
az webapp config appsettings set --name "your-app-name" --resource-group "rg-fishing-regs" --settings AZURE_KEY_VAULT_URI="https://kv-fishing-regs-prod.vault.azure.net/"

# For local testing with service principal
$env:AZURE_KEY_VAULT_URI="https://kv-fishing-regs-prod.vault.azure.net/"
$env:AZURE_CLIENT_ID="your-service-principal-client-id"
$env:AZURE_CLIENT_SECRET="your-service-principal-secret"
$env:AZURE_TENANT_ID="your-tenant-id"
```

## How It Works

### Configuration Priority

The `SecureConfigurationExtensions` class follows this priority order:

1. **Azure Key Vault** (if `AZURE_KEY_VAULT_URI` environment variable is set)
2. **User Secrets** (if running in Development environment)
3. **Environment Variables** (fallback)
4. **appsettings.json** (base configuration, no secrets)

### Code Integration

Services are registered using the secure configuration:

```csharp
// In Program.cs or Startup.cs
builder.Services.AddSecureConfiguration(builder.Configuration);
builder.Services.AddPdfProcessingServices();
```

The `SecureConfigurationExtensions` automatically:
- Detects the environment
- Loads appropriate credential source
- Validates required settings
- Provides helpful error messages with setup instructions

### Environment Detection

- **Development**: Uses User Secrets when `ASPNETCORE_ENVIRONMENT=Development`
- **Production**: Uses Key Vault when `AZURE_KEY_VAULT_URI` is set
- **Fallback**: Uses environment variables in all other cases

## Troubleshooting

### Common Issues

1. **Missing Configuration Error**
   - Verify secrets are set correctly
   - Check environment variable names match exactly
   - Ensure User Secrets are initialized

2. **Key Vault Access Denied**
   - Verify the application identity has access to Key Vault
   - Check that the Key Vault URI is correct
   - Ensure secret names use `--` instead of `:`

3. **Development Environment Not Detected**
   - Set `ASPNETCORE_ENVIRONMENT=Development`
   - Verify User Secrets are properly initialized

### Validation Commands

```powershell
# Check User Secrets
dotnet user-secrets list

# Test Key Vault access (requires Azure CLI login)
az keyvault secret show --vault-name "kv-fishing-regs-prod" --name "AzureAI--DocumentIntelligence--Endpoint"

# Test application configuration
dotnet run  # Should show configuration status
```

## Security Best Practices

1. **Never commit secrets to source control**
2. **Use least-privilege access for Key Vault**
3. **Rotate API keys regularly**
4. **Monitor Key Vault access logs**
5. **Use separate Key Vaults for different environments**
6. **Enable Key Vault soft delete and purge protection**

## Next Steps

Once configuration is set up:
1. Test the PDF processing service with real documents
2. Integrate with the main Blazor application
3. Set up monitoring and logging for production use
4. Configure CI/CD pipelines with secure deployment

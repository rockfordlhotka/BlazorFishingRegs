# Environment Configuration & Secrets Management

This directory contains scripts and configuration files for managing environment variables and secrets for the Blazor Fishing Regulations application across different environments (Development, Staging, Production).

## 📁 Directory Structure

```
src/
├── config/
│   ├── environments/
│   │   ├── .env.template          # Template for environment variables
│   │   ├── .env.development       # Development environment variables
│   │   └── .env.production        # Production environment variables
│   ├── azure-keyvault-template.json   # Azure Key Vault configuration template
│   └── user-secrets-template.json     # .NET User Secrets template
└── scripts/
    ├── setup-dev-environment.ps1      # Development environment setup
    ├── setup-user-secrets.ps1         # .NET User Secrets configuration
    ├── setup-azure-keyvault.ps1       # Azure Key Vault setup
    └── validate-environment.ps1       # Environment validation
```

## 🚀 Quick Start

### For Development (Local)

1. **Set up development environment:**
   ```powershell
   .\src\scripts\setup-dev-environment.ps1 -Local
   ```

2. **Start Docker containers:**
   ```powershell
   docker-compose up -d
   ```

3. **Validate environment:**
   ```powershell
   .\src\scripts\validate-environment.ps1 -Environment Development
   ```

### For Production (Azure)

1. **Set up Azure Key Vault:**
   ```powershell
   .\src\scripts\setup-azure-keyvault.ps1 -KeyVaultName "fishing-regs-kv" -ResourceGroupName "fishing-regs-rg" -CreateKeyVault -Interactive
   ```

2. **Validate production environment:**
   ```powershell
   .\src\scripts\validate-environment.ps1 -Environment Production -KeyVaultName "fishing-regs-kv"
   ```

## 📋 Detailed Setup Instructions

### 1. Development Environment Setup

#### Option A: Automated Setup (Recommended)
```powershell
# Set up local development with mock services
.\src\scripts\setup-dev-environment.ps1 -Local

# OR set up with Azure services
.\src\scripts\setup-dev-environment.ps1 -Azure
```

#### Option B: Manual Setup

1. **Copy environment template:**
   ```powershell
   Copy-Item "src\config\environments\.env.development" ".env"
   ```

2. **Set up .NET User Secrets:**
   ```powershell
   .\src\scripts\setup-user-secrets.ps1
   ```

3. **Update configuration as needed**

### 2. Production Environment Setup

#### Prerequisites
- Azure CLI installed and configured
- Azure subscription with appropriate permissions
- Azure resources created (Key Vault, App Service, etc.)

#### Step-by-Step Setup

1. **Create Azure Key Vault:**
   ```powershell
   .\src\scripts\setup-azure-keyvault.ps1 `
       -KeyVaultName "fishing-regs-kv" `
       -ResourceGroupName "fishing-regs-rg" `
       -Location "East US" `
       -CreateKeyVault `
       -Interactive
   ```

2. **Configure App Service to use Key Vault:**
   - Enable Managed Identity on your App Service
   - Grant Key Vault access to the Managed Identity
   - Configure app settings to reference Key Vault secrets

3. **Validate configuration:**
   ```powershell
   .\src\scripts\validate-environment.ps1 -Environment Production -KeyVaultName "fishing-regs-kv"
   ```

## 🔐 Required Secrets and Environment Variables

### Development Environment

| Variable | Description | Example |
|----------|-------------|---------|
| `SQL_CONNECTION_STRING` | Local SQL Server connection | `Server=localhost,1433;Database=FishingRegsDB;...` |
| `REDIS_CONNECTION_STRING` | Local Redis connection | `localhost:6379` |
| `AZURE_STORAGE_CONNECTION_STRING` | Azurite local storage | `UseDevelopmentStorage=true` |
| `USE_MOCK_AI_SERVICES` | Enable mock AI services | `true` |
| `MOCK_AI_SERVICE_URL` | Mock AI service endpoint | `http://localhost:7000` |

### Production Environment

| Secret Name (Key Vault) | Description | Configuration Key |
|--------------------------|-------------|-------------------|
| `azure-openai-endpoint` | Azure OpenAI service URL | `AzureOpenAI:Endpoint` |
| `azure-openai-api-key` | Azure OpenAI API key | `AzureOpenAI:ApiKey` |
| `azure-document-intelligence-endpoint` | Document Intelligence URL | `AzureDocumentIntelligence:Endpoint` |
| `azure-document-intelligence-api-key` | Document Intelligence API key | `AzureDocumentIntelligence:ApiKey` |
| `sql-connection-string` | Production SQL Server connection | `ConnectionStrings:DefaultConnection` |
| `redis-connection-string` | Azure Cache for Redis connection | `ConnectionStrings:Redis` |
| `azure-storage-connection-string` | Azure Storage connection | `ConnectionStrings:AzureStorage` |
| `fishing-regs-api-key` | Application API key | `ApplicationSettings:ApiKey` |
| `fishing-regs-jwt-secret` | JWT signing secret | `ApplicationSettings:JwtSecret` |

## 🛠️ Script Usage

### setup-dev-environment.ps1

Sets up development environment variables and creates `.env` file for Docker Compose.

```powershell
# Local development setup
.\setup-dev-environment.ps1 -Local

# Azure services setup
.\setup-dev-environment.ps1 -Azure

# Reset and reconfigure
.\setup-dev-environment.ps1 -Reset -Local
```

**Parameters:**
- `-Local`: Configure for local development (default)
- `-Azure`: Configure for Azure services
- `-Reset`: Clear existing environment variables

### setup-user-secrets.ps1

Configures .NET User Secrets for the Blazor application.

```powershell
# Set up development secrets
.\setup-user-secrets.ps1

# Set up Azure service secrets
.\setup-user-secrets.ps1 -Azure

# Reset and reconfigure
.\setup-user-secrets.ps1 -Reset
```

**Parameters:**
- `-ProjectPath`: Path to .csproj file (default: `src/BlazorFishingRegs`)
- `-Azure`: Configure Azure service secrets
- `-Reset`: Clear existing user secrets

### setup-azure-keyvault.ps1

Creates and configures Azure Key Vault for production secrets.

```powershell
# Create new Key Vault and set up secrets interactively
.\setup-azure-keyvault.ps1 -KeyVaultName "fishing-regs-kv" -ResourceGroupName "fishing-regs-rg" -CreateKeyVault -Interactive

# Set up secrets in existing Key Vault with templates
.\setup-azure-keyvault.ps1 -KeyVaultName "existing-kv"
```

**Parameters:**
- `-KeyVaultName`: Name of the Key Vault (required)
- `-ResourceGroupName`: Azure resource group name
- `-CreateKeyVault`: Create a new Key Vault
- `-Interactive`: Prompt for all secret values
- `-Location`: Azure region for Key Vault

### validate-environment.ps1

Validates environment configuration and tests connections.

```powershell
# Validate development environment
.\validate-environment.ps1 -Environment Development

# Validate production with connection tests
.\validate-environment.ps1 -Environment Production -KeyVaultName "fishing-regs-kv" -CheckConnections
```

**Parameters:**
- `-Environment`: Target environment (Development, Staging, Production)
- `-KeyVaultName`: Key Vault name for production validation
- `-CheckConnections`: Test actual service connections

## 🔄 Environment Variable Precedence

The application follows .NET's configuration precedence (highest to lowest):

1. **Command-line arguments**
2. **Environment variables**
3. **User secrets** (Development only)
4. **appsettings.{Environment}.json**
5. **appsettings.json**
6. **Azure Key Vault** (Production)

## 🐳 Docker Configuration

### Environment Files

- **`.env`**: Created by setup scripts, contains all Docker Compose variables
- **`.env.development`**: Development-specific variables
- **`.env.production`**: Production template (do not commit with real secrets)

### Docker Compose Variables

Key environment variables used in `docker-compose.yml`:

```yaml
services:
  blazor-fishing-app:
    environment:
      - ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT}
      - SQL_CONNECTION_STRING=${SQL_CONNECTION_STRING}
      - REDIS_CONNECTION_STRING=${REDIS_CONNECTION_STRING}
      - AZURE_STORAGE_CONNECTION_STRING=${AZURE_STORAGE_CONNECTION_STRING}
      # ... other variables
```

## 🔒 Security Best Practices

### Development
- ✅ Use local services (SQL Server, Redis, Azurite) in Docker containers
- ✅ Use mock AI services to avoid Azure costs and API limits
- ✅ Store secrets in .NET User Secrets (not in source control)
- ✅ Use different passwords/keys than production

### Production
- ✅ Use Azure Key Vault for all secrets
- ✅ Enable Managed Identity for App Service
- ✅ Use Azure AD authentication for databases when possible
- ✅ Set restrictive CORS and allowed hosts
- ✅ Use strong, unique secrets (minimum 256-bit for JWT)
- ✅ Enable SSL/TLS for all connections
- ❌ Never commit production secrets to source control
- ❌ Never use wildcard (*) for allowed hosts in production

## 🧪 Testing and Validation

### Manual Testing

```powershell
# Test development environment
docker-compose up -d
curl https://localhost:8443/health

# Test environment validation
.\src\scripts\validate-environment.ps1 -Environment Development -CheckConnections
```

### Automated Testing

The validation script generates a JSON report with all check results:

```json
{
  "Component": "Database",
  "Check": "Environment Variable: SQL_CONNECTION_STRING",
  "Passed": true,
  "Message": "Set correctly",
  "Value": "***"
}
```

## ❗ Troubleshooting

### Common Issues

1. **Docker containers not starting:**
   ```powershell
   docker-compose logs
   docker-compose down && docker-compose up -d
   ```

2. **Environment variables not loading:**
   ```powershell
   # Check .env file exists
   Get-Content .env
   
   # Verify user secrets
   dotnet user-secrets list
   ```

3. **Azure Key Vault access denied:**
   ```powershell
   # Check access policies
   az keyvault show --name "your-keyvault" --query "properties.accessPolicies"
   
   # Grant access to current user
   az keyvault set-policy --name "your-keyvault" --upn "your-email@domain.com" --secret-permissions get list
   ```

4. **SSL certificate issues:**
   ```powershell
   # Generate development certificate
   dotnet dev-certs https --trust
   ```

### Getting Help

1. **Validate your environment:**
   ```powershell
   .\src\scripts\validate-environment.ps1 -Environment [Your-Environment]
   ```

2. **Check the validation report** generated in the root directory

3. **Review container logs:**
   ```powershell
   docker-compose logs [service-name]
   ```

## 📚 Additional Resources

- [.NET Configuration Documentation](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/)
- [Azure Key Vault Documentation](https://docs.microsoft.com/en-us/azure/key-vault/)
- [Docker Compose Environment Variables](https://docs.docker.com/compose/environment-variables/)
- [ASP.NET Core User Secrets](https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets)

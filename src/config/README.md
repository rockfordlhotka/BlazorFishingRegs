# Environment Configuration & Secrets Management

This directory contains scripts and configuration files for managing environment variables and secrets for the Blazor Fishing Regulations application using **.NET Aspire** orchestration.

## 📁 Directory Structure

```
src/
├── FishingRegs.AppHost/                # .NET Aspire orchestration project
│   ├── Program.cs                      # Service definitions and configuration
│   └── FishingRegs.AppHost.csproj     
├── FishingRegs.ServiceDefaults/        # Shared telemetry and configurations
│   ├── Extensions.cs                   # OpenTelemetry, health checks
│   └── FishingRegs.ServiceDefaults.csproj
├── config/
│   ├── environments/
│   │   └── aspire-configuration.template  # Aspire configuration template
│   ├── azure-keyvault-template.json       # Azure Key Vault configuration template
│   └── user-secrets-template.json         # .NET User Secrets template
└── scripts/
    ├── setup.ps1                          # Main setup script (Aspire-focused)
    ├── setup-dev-environment.ps1          # Development environment setup
    ├── setup-aspire.ps1                   # Aspire workload installation
    ├── setup-user-secrets.ps1             # .NET User Secrets configuration
    ├── setup-azure-keyvault.ps1           # Azure Key Vault setup
    └── validate-environment.ps1           # Environment validation
```

## 🚀 Quick Start

### For Development (Local)

1. **Set up Aspire development environment:**
   ```powershell
   .\src\scripts\setup.ps1
   ```

2. **Start Aspire application:**
   ```powershell
   dotnet run --project src\FishingRegs.AppHost
   ```

3. **Open Aspire Dashboard:**
   - Dashboard opens automatically at http://localhost:15888
   - All services are visible with health status, logs, and metrics

### For Production (Azure)

1. **Set up Azure Key Vault:**
   ```powershell
   .\src\scripts\setup-azure-keyvault.ps1 -KeyVaultName "fishing-regs-kv" -ResourceGroupName "fishing-regs-rg" -CreateKeyVault -Interactive
   ```

2. **Configure Aspire for production:**
   ```powershell
   .\src\scripts\setup-dev-environment.ps1 -Environment Azure
   ```

## 📋 Detailed Setup Instructions

### 1. Development Environment Setup

#### Automated Setup (Recommended)
```powershell
# Set up complete development environment with Aspire
.\src\scripts\setup.ps1

# OR set up with Azure services
.\src\scripts\setup.ps1 -Environment Azure
```

This will:
- Install .NET Aspire workload
- Configure environment variables
- Set up user secrets for Aspire projects
- Validate the configuration

#### Manual Setup

1. **Install Aspire workload:**
   ```powershell
   dotnet workload install aspire
   ```

2. **Set up environment:**
   ```powershell
   .\src\scripts\setup-dev-environment.ps1
   ```

3. **Configure Aspire secrets:**
   ```powershell
   .\src\scripts\setup-user-secrets.ps1
   ```

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

### .NET Aspire Service Configuration

| Service | Aspire Integration | Configuration |
|---------|-------------------|--------------|
| **SQL Server** | `builder.AddSqlServer("sql").AddDatabase("FishingRegsDB")` | Automatic connection strings |
| **Redis Cache** | `builder.AddRedis("redis")` | Automatic service discovery |
| **Azure Storage** | `builder.AddAzureStorage("storage").AddBlobs("documents")` | Local: Azurite, Prod: Azure Storage |
| **Seq Logging** | `builder.AddSeq("seq")` | Automatic log forwarding |
| **Azure OpenAI** | `builder.AddAzureOpenAI("openai").AddDeployment("gpt-4")` | API key via user secrets |
| **Document Intelligence** | Custom integration with health checks | API key via user secrets |

### Development Environment (Aspire Managed)

All services are automatically configured by Aspire with service discovery and health checks.

| Component | How It's Managed |
|-----------|------------------|
| Database | Aspire SQL Server container with data persistence |
| Cache | Aspire Redis container with data persistence |
| Storage | Azurite (Azure Storage Emulator) |
| AI Services | Mock service container |
| Logging | Seq container with dashboard integration |
| Monitoring | Built-in OpenTelemetry and Aspire dashboard |

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

### setup.ps1

Main entry point for setting up the complete Aspire development environment.

```powershell
# Set up development environment with Aspire
.\setup.ps1

# Set up with Azure services
.\setup.ps1 -Environment Azure

# Reset and reconfigure
.\setup.ps1 -Reset
```

**What it does:**
- Installs .NET Aspire workload
- Configures environment variables
- Sets up user secrets for Aspire projects
- Validates the configuration

### setup-aspire.ps1

Installs and configures the .NET Aspire workload.

```powershell
# Install Aspire workload and validate setup
.\setup-aspire.ps1

# Skip workload installation if already installed
.\setup-aspire.ps1 -SkipWorkloadInstall
```

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

With .NET Aspire, configuration follows this precedence (highest to lowest):

1. **Aspire service integrations** (automatic service discovery)
2. **User secrets** (development)
3. **Environment variables**
4. **appsettings.{Environment}.json**
5. **appsettings.json**
6. **Azure Key Vault** (production)

## � .NET Aspire Benefits

### vs Traditional Docker Compose

| Aspect | Docker Compose | .NET Aspire |
|--------|----------------|-------------|
| **Startup** | `docker-compose up -d` (30-45s) | `dotnet run` (15-20s) |
| **Services** | Manual container configuration | Automatic service discovery |
| **Monitoring** | External tools (Seq, etc.) | Built-in dashboard |
| **Development** | Container debugging | Native .NET debugging |
| **Hot Reload** | Limited support | Full .NET hot reload |
| **Azure Integration** | Manual configuration | Native Azure service integrations |

### Development Workflow

```bash
# Single command starts everything
dotnet run --project src\FishingRegs.AppHost

# Aspire Dashboard opens automatically
# - View all services and their health
# - Access logs from all components
# - Monitor metrics and traces
# - Direct links to service endpoints
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
# Start Aspire application
dotnet run --project src\FishingRegs.AppHost

# Check health through Aspire dashboard
# http://localhost:15888

# Test environment validation
.\src\scripts\validate-environment.ps1 -Environment Development
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

1. **Aspire workload not installed:**
   ```powershell
   # Install the workload
   dotnet workload install aspire
   
   # Verify installation
   dotnet workload list
   ```

2. **Services not starting:**
   ```powershell
   # Check Aspire logs in dashboard
   # http://localhost:15888
   
   # Or check console output
   dotnet run --project src\FishingRegs.AppHost --verbosity normal
   ```

3. **Environment variables not loading:**
   ```powershell
   # Check user secrets for AppHost project
   cd src\FishingRegs.AppHost
   dotnet user-secrets list
   ```

4. **Azure service access issues:**
   ```powershell
   # Verify Azure CLI authentication
   az account show
   
   # Check Key Vault access policies
   az keyvault show --name "your-keyvault" --query "properties.accessPolicies"
   ```

### Getting Help

1. **Validate your environment:**
   ```powershell
   .\src\scripts\validate-environment.ps1 -Environment [Your-Environment]
   ```

2. **Check the Aspire dashboard** at http://localhost:15888

3. **Review service logs** in the Aspire dashboard or console output

## 📚 Additional Resources

- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Azure Service Integrations](https://learn.microsoft.com/en-us/dotnet/aspire/azure/)
- [.NET Configuration Documentation](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/)
- [Azure Key Vault Documentation](https://docs.microsoft.com/en-us/azure/key-vault/)
- [ASP.NET Core User Secrets](https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets)

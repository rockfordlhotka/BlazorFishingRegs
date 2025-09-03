# 🎣 Blazor Fishing Regulations - .NET Aspire Setup

Welcome! This project uses **.NET Aspire** for orchestration and development. Follow these simple steps to get started.

## 🚀 Quick Start (5 minutes)

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for local services)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/)

### Setup

1. **Clone and setup environment:**
   ```powershell
   git clone <repository-url>
   cd BlazorAI-spec
   .\src\scripts\setup.ps1
   ```

2. **Start the application:**
   ```powershell
   dotnet run --project src\FishingRegs.AppHost
   ```

3. **Open the Aspire Dashboard:**
   - Automatically opens at http://localhost:15888
   - View all services, logs, metrics, and health status

That's it! 🎉

## 🔧 What .NET Aspire Provides

### Automatic Service Management
- **SQL Server** - Database with data persistence
- **Redis** - Caching with automatic discovery
- **Azurite** - Azure Storage emulator for PDFs
- **Seq** - Centralized logging with dashboard
- **Mock AI Service** - Development AI service simulation

### Built-in Features
- ✅ **Service Discovery** - Automatic connection configuration
- ✅ **Health Checks** - Real-time service monitoring
- ✅ **Telemetry** - OpenTelemetry metrics and tracing
- ✅ **Hot Reload** - Fast development iteration
- ✅ **Debugging** - Native .NET debugging across services

## 🎯 Development Workflow

```powershell
# Single command starts everything (15-20 seconds)
dotnet run --project src\FishingRegs.AppHost

# Aspire Dashboard shows:
# - All service status and health
# - Logs from all components  
# - Metrics and distributed tracing
# - Direct links to service endpoints
```

## 🔐 Configuration

### Development (Automatic)
All services are automatically configured by Aspire with:
- Database connections and migrations
- Cache configuration and discovery
- Storage emulation (Azurite)
- Logging aggregation (Seq)
- Mock AI services

### Azure Services (When Ready)
```powershell
# Configure for Azure services
.\src\scripts\setup.ps1 -Environment Azure

# Uses real Azure OpenAI, Document Intelligence, etc.
```

## 📁 Project Structure

```
src/
├── FishingRegs.AppHost/           # 🎯 Aspire orchestration
├── FishingRegs.ServiceDefaults/   # 📊 Shared telemetry
├── BlazorFishingRegs/             # 🌐 Main Blazor app
├── FishingRegs.AIMockService/     # 🤖 AI service simulation
├── config/                        # ⚙️ Configuration templates
└── scripts/                       # 🔧 Setup and utility scripts
```

## 🆚 Why Aspire vs Docker Compose?

| Feature | Docker Compose | .NET Aspire |
|---------|----------------|-------------|
| **Setup** | Complex scripts + containers | Single command |
| **Startup** | 30-45 seconds | 15-20 seconds |
| **Debugging** | Container logs | Native .NET debugging |
| **Service Discovery** | Manual configuration | Automatic |
| **Monitoring** | External tools | Built-in dashboard |
| **Hot Reload** | Limited | Full .NET support |
| **Azure Integration** | Manual setup | Native integrations |

## 🔧 Commands Reference

```powershell
# Setup complete environment
.\src\scripts\setup.ps1

# Setup with Azure services  
.\src\scripts\setup.ps1 -Environment Azure

# Install/update Aspire workload
.\src\scripts\setup-aspire.ps1

# Start application
dotnet run --project src\FishingRegs.AppHost

# Validate environment
.\src\scripts\validate-environment.ps1

# Configure Azure Key Vault (production)
.\src\scripts\setup-azure-keyvault.ps1 -KeyVaultName "fishing-regs-kv" -Interactive
```

## 🚀 Next Steps

After setup:

1. **Explore the Aspire Dashboard** - http://localhost:15888
2. **Start building the Blazor app** - Phase 2 of implementation
3. **Configure Azure services** when ready for production

## 🆘 Troubleshooting

**Aspire not starting?**
```powershell
# Check if workload is installed
dotnet workload list

# Install if missing
dotnet workload install aspire
```

**Services not healthy?**
- Check the Aspire Dashboard for detailed health information
- Review service logs in the dashboard
- Ensure Docker Desktop is running

**Need help?** Check `src\config\README.md` for detailed documentation.

---

🎣 **Happy fishing (regulation) development!** 🎣

# PostgreSQL Migration Guide

This document explains the migration from SQL Server to PostgreSQL for the Blazor Fishing Regulations application.

## 🐘 Why PostgreSQL?

- **Azure-native**: Excellent integration with Azure Database for PostgreSQL
- **Cost-effective**: Generally more cost-effective than SQL Server on Azure
- **Open source**: No licensing costs
- **Performance**: Excellent performance for read-heavy workloads
- **JSON support**: Native JSON data types for flexible data storage
- **Extensions**: Rich ecosystem of extensions (PostGIS for geographic data)

## 🔧 Configuration Changes Made

### 1. AppHost Project Updates
- **Package**: Changed from `Aspire.Hosting.SqlServer` to `Aspire.Hosting.PostgreSQL`
- **Service**: Updated `AddSqlServer()` to `AddPostgres()` in `Program.cs`
- **Database**: PostgreSQL container for local development

### 2. Connection Strings
- **Local**: `Host=localhost;Port=5432;Database=FishingRegsDB;Username=postgres;Password=FishingRegs2025!`
- **Azure**: `Host=your-server.postgres.database.azure.com;Port=5432;Database=FishingRegsDB;Username=admin@server;Password=password;SslMode=Require`

### 3. CI/CD Updates
- **GitHub Actions**: Added PostgreSQL service containers for testing
- **Build**: Updated workflows to test against PostgreSQL 15

## 🚀 Local Development

### Using .NET Aspire (Recommended)
```powershell
# Start the application (PostgreSQL runs in container)
dotnet run --project src\FishingRegs.AppHost

# Aspire Dashboard shows PostgreSQL health and connection info
# http://localhost:15888
```

### Manual PostgreSQL Setup (Optional)
```powershell
# Using Docker directly
docker run --name fishing-regs-postgres -e POSTGRES_PASSWORD=FishingRegs2025! -e POSTGRES_DB=FishingRegsDB -p 5432:5432 -d postgres:15

# Connect using psql
psql -h localhost -U postgres -d FishingRegsDB
```

## ☁️ Azure Deployment

### 1. Create Azure Database for PostgreSQL
```powershell
# Use the provided script
.\src\scripts\setup-azure-postgresql.ps1 `
    -ResourceGroupName "fishing-regs-rg" `
    -ServerName "fishing-regs-db" `
    -AdminUsername "fishadmin" `
    -AdminPassword (ConvertTo-SecureString "YourSecurePassword123!" -AsPlainText -Force)
```

### 2. Configure Connection String
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=fishing-regs-db.postgres.database.azure.com;Port=5432;Database=FishingRegsDB;Username=fishadmin@fishing-regs-db;Password=YourSecurePassword123!;SslMode=Require;Trust Server Certificate=true"
  }
}
```

### 3. Security Configuration
- **SSL**: Always required for Azure PostgreSQL
- **Firewall**: Configure IP rules for your application
- **Authentication**: Consider Azure AD authentication for production

## 🔄 Migration Steps (When You Have Existing Data)

### From SQL Server to PostgreSQL
1. **Schema Migration**:
   ```bash
   # Export SQL Server schema
   sqlcmd -S server -d database -Q "script schema" > schema.sql
   
   # Convert to PostgreSQL (manual process or tools like pgloader)
   ```

2. **Data Migration**:
   ```bash
   # Use pgloader or custom ETL process
   pgloader mssql://user:pass@server/database postgresql://user:pass@server/database
   ```

3. **Entity Framework**:
   ```csharp
   // Update packages
   // Remove: Microsoft.EntityFrameworkCore.SqlServer
   // Add: Npgsql.EntityFrameworkCore.PostgreSQL
   
   // Update DbContext configuration
   services.AddDbContext<AppDbContext>(options =>
       options.UseNpgsql(connectionString));
   ```

## 🛠️ Development Tools

### Database Management
- **pgAdmin**: Web-based PostgreSQL administration
- **Azure Data Studio**: Cross-platform database tool with PostgreSQL support
- **DBeaver**: Universal database tool

### Visual Studio Code Extensions
- **PostgreSQL**: Syntax highlighting and connection management
- **Database Client**: Multi-database support including PostgreSQL

## 📊 Monitoring & Performance

### Azure PostgreSQL Insights
- **Performance**: Built-in performance monitoring
- **Slow Query Log**: Identify performance bottlenecks
- **Connection Pooling**: Use pgBouncer for high-concurrency scenarios

### Application Monitoring
- **OpenTelemetry**: Already configured in ServiceDefaults
- **Health Checks**: PostgreSQL health checks via Aspire
- **Metrics**: Connection pool metrics and query performance

## 🔐 Security Best Practices

### Connection Security
- **SSL/TLS**: Always use encrypted connections
- **Connection Strings**: Store in Azure Key Vault
- **Least Privilege**: Use database roles with minimal permissions

### Azure-Specific
- **Private Endpoints**: For production environments
- **Azure AD Authentication**: Eliminate password-based authentication
- **Network Security**: Virtual network integration

## 🆘 Troubleshooting

### Common Issues
1. **Connection Timeouts**: Check firewall rules and network connectivity
2. **SSL Certificate**: Use `Trust Server Certificate=true` for Azure PostgreSQL
3. **Authentication**: Verify username format (admin@servername for Azure)
4. **Connection Pooling**: Monitor pool exhaustion in high-load scenarios

### Debugging Tools
```powershell
# Test connection
psql "Host=server;Port=5432;Database=db;Username=user;Password=pass;SslMode=Require"

# Check server status
az postgres flexible-server show --resource-group rg --name server

# View server logs
az postgres flexible-server server-logs list --resource-group rg --name server
```

## 📚 Additional Resources

- [Azure Database for PostgreSQL Documentation](https://docs.microsoft.com/en-us/azure/postgresql/)
- [Npgsql Entity Framework Core Provider](https://www.npgsql.org/efcore/)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)
- [.NET Aspire PostgreSQL Component](https://learn.microsoft.com/en-us/dotnet/aspire/database/postgresql-component)

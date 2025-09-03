# Fishing Regulations Docker Setup

This directory contains the Docker configuration for running the Blazor AI Fishing Regulations application locally using Docker Compose.

## Quick Start

1. **Prerequisites**
   ```bash
   # Install Docker and Docker Compose
   # Windows: Docker Desktop
   # Linux: Docker Engine + Docker Compose
   # macOS: Docker Desktop
   ```

2. **Clone and Setup**
   ```bash
   cd s:\src\rdl\BlazorAI-spec
   
   # Generate HTTPS certificate for development
   dotnet dev-certs https -ep ~/.aspnet/https/aspnetapp.pfx -p fishingdev123
   dotnet dev-certs https --trust
   ```

3. **Start the Application**
   ```bash
   # Start all services
   docker-compose up -d
   
   # View logs
   docker-compose logs -f blazor-fishing-app
   
   # Check service status
   docker-compose ps
   ```

4. **Access the Application**
   - **Main App**: https://localhost:443 or http://localhost:80
   - **Direct App**: https://localhost:8443 or http://localhost:8080
   - **Seq Logs**: http://localhost:8081/seq
   - **Storage UI**: http://localhost:8081/storage

## Services Overview

| Service | Purpose | Port | Health Check |
|---------|---------|------|--------------|
| blazor-fishing-app | Main Blazor Server app | 8080, 8443 | /health |
| sql-server | SQL Server database | 1433 | SQL connection test |
| redis | Cache for regulations | 6379 | Redis ping |
| azurite | Local Azure Storage | 10000-10002 | Blob service |
| seq | Centralized logging | 5341, 8081 | HTTP endpoint |
| nginx | Reverse proxy/SSL | 80, 443 | Config reload |
| ai-mock-service | Mock AI service | 7000 | /health |

## Development Commands

```bash
# Full environment
docker-compose up -d

# Only database and cache
docker-compose up -d sql-server redis azurite

# View application logs
docker-compose logs -f blazor-fishing-app

# Rebuild application container
docker-compose build blazor-fishing-app
docker-compose up -d blazor-fishing-app

# Database operations
docker-compose exec sql-server /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P FishingRegs2025!

# Redis operations
docker-compose exec redis redis-cli

# Stop all services
docker-compose down

# Stop and remove volumes (reset data)
docker-compose down -v
```

## Configuration

### Environment Variables

The application uses these key environment variables:

```bash
# Database
ConnectionStrings__DefaultConnection=Server=sql-server,1433;Database=FishingRegulations_Dev;...

# Cache
ConnectionStrings__Redis=redis:6379

# Storage
AzureStorage__ConnectionString=DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;...

# Logging
Seq__ServerUrl=http://seq:5341

# AI Services
AI__UseAzureServices=false
AI__MockService__BaseUrl=http://ai-mock-service:7000
```

### Volume Mounts

- `./data` → `/app/data` (fishing regulations PDFs)
- `./logs` → `/app/logs` (application logs)
- `sql-data` → `/var/opt/mssql` (database persistence)
- `redis-data` → `/data` (cache persistence)

## Troubleshooting

### Common Issues

1. **Port Conflicts**
   ```bash
   # Check what's using the ports
   netstat -an | findstr :1433
   netstat -an | findstr :6379
   
   # Modify ports in docker-compose.yml if needed
   ```

2. **Database Connection Issues**
   ```bash
   # Check SQL Server status
   docker-compose logs sql-server
   
   # Test connection manually
   docker-compose exec sql-server /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P FishingRegs2025!
   ```

3. **SSL Certificate Issues**
   ```bash
   # Regenerate development certificates
   dotnet dev-certs https --clean
   dotnet dev-certs https -ep ~/.aspnet/https/aspnetapp.pfx -p fishingdev123
   dotnet dev-certs https --trust
   ```

4. **Storage Issues**
   ```bash
   # Check Azurite logs
   docker-compose logs azurite
   
   # Reset storage data
   docker-compose down
   docker volume rm blazorai-spec_azurite-data
   docker-compose up -d azurite
   ```

### Performance Tuning

1. **Memory Allocation**
   - SQL Server: 2GB default
   - Redis: 256MB default
   - Blazor App: Unlimited (limited by host)

2. **Database Optimization**
   ```sql
   -- Check database size
   SELECT 
       DB_NAME(database_id) AS DatabaseName,
       (size * 8) / 1024 AS SizeMB
   FROM sys.master_files
   ```

3. **Cache Optimization**
   ```bash
   # Redis memory usage
   docker-compose exec redis redis-cli info memory
   
   # Clear cache if needed
   docker-compose exec redis redis-cli flushall
   ```

## Production Considerations

For production deployment:

1. **Security**
   - Change default passwords
   - Use proper SSL certificates
   - Enable authentication for Redis
   - Configure firewall rules

2. **Performance**
   - Use production SQL Server instance
   - Configure Redis clustering
   - Use Azure Storage instead of Azurite
   - Enable real Azure AI services

3. **Monitoring**
   - Configure Application Insights
   - Set up alerting rules
   - Monitor resource usage
   - Enable detailed logging

## File Structure

```
s:\src\rdl\BlazorAI-spec\
├── docker-compose.yml              # Main compose file
├── docker-compose.override.yml     # Development overrides
├── nginx/
│   ├── nginx.conf                  # Nginx configuration
│   └── ssl/                        # SSL certificates
├── redis/
│   └── redis.conf                  # Redis configuration
├── data/                           # Sample fishing regulations data
├── logs/                           # Application logs
└── src/
    ├── BlazorFishingRegs/
    │   └── Dockerfile              # Main app Dockerfile
    └── AIMockService/
        └── Dockerfile              # Mock AI service Dockerfile
```

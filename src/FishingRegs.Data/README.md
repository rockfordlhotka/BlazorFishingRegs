# FishingRegs.Data - Data Access Layer

This class library implements the data access layer for the Fishing Regulations application using the Repository pattern with Entity Framework Core and PostgreSQL.

## Features

- **Repository Pattern**: Clean separation of data access logic with generic and specific repository interfaces
- **Unit of Work**: Coordinates repository operations and manages transactions
- **PostgreSQL Support**: Optimized for PostgreSQL with proper indexing and data types
- **Dependency Injection**: Easy integration with .NET DI container
- **Health Checks**: Built-in database health monitoring
- **Entity Framework Core**: Modern ORM with migrations and seeding

## Project Structure

```
FishingRegs.Data/
├── Models/                          # Entity models
│   ├── CoreEntities.cs             # State, County, FishSpecies
│   ├── WaterBody.cs                # Water body and species junction
│   ├── FishingRegulation.cs        # Fishing regulations
│   ├── RegulationDocument.cs       # PDF documents
│   ├── User.cs                     # User and favorites
│   └── Analytics.cs                # Search history and audit logs
├── Repositories/                   # Repository interfaces and implementations
│   ├── IRepository.cs              # Generic repository interface
│   ├── IWaterBodyRepository.cs     # Water body specific operations
│   ├── IFishingRegulationRepository.cs
│   ├── IRegulationDocumentRepository.cs
│   ├── ILookupRepositories.cs      # State, County, FishSpecies
│   └── Implementation/             # Concrete implementations
│       ├── Repository.cs           # Base repository
│       ├── WaterBodyRepository.cs
│       ├── FishingRegulationRepository.cs
│       ├── RegulationDocumentRepository.cs
│       └── LookupRepositories.cs
├── Extensions/
│   └── ServiceCollectionExtensions.cs  # DI registration
├── FishingRegsDbContext.cs         # EF Core DbContext
├── IUnitOfWork.cs                  # Unit of Work interface
└── UnitOfWork.cs                   # Unit of Work implementation
```

## Getting Started

### 1. Add Package Reference

```xml
<PackageReference Include="FishingRegs.Data" Version="1.0.0" />
```

### 2. Configure Services

In your `Program.cs` or `Startup.cs`:

```csharp
using FishingRegs.Data.Extensions;

// Add data access layer with connection string
builder.Services.AddDataAccessLayer(builder.Configuration);

// Or with custom configuration
builder.Services.AddDataAccessLayer(options =>
{
    options.UseNpgsql(connectionString);
});

// Add health checks (optional)
builder.Services.AddDataAccessHealthChecks();
```

### 3. Connection String

Add to your `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=fishing_regs;Username=postgres;Password=your_password"
  }
}
```

### 4. Database Setup

```csharp
// In Program.cs, ensure database is created and seeded
await app.Services.EnsureDatabaseAsync();
await app.Services.SeedDatabaseAsync();
```

## Usage Examples

### Using Unit of Work

```csharp
public class FishingService
{
    private readonly IUnitOfWork _unitOfWork;

    public FishingService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<WaterBody>> GetLakesByStateAsync(string stateCode)
    {
        var state = await _unitOfWork.States.GetByCodeAsync(stateCode);
        if (state == null) return Enumerable.Empty<WaterBody>();

        return await _unitOfWork.WaterBodies.GetByStateAsync(state.Id);
    }

    public async Task<WaterBody> CreateWaterBodyAsync(WaterBody waterBody)
    {
        var result = await _unitOfWork.WaterBodies.AddAsync(waterBody);
        await _unitOfWork.SaveChangesAsync();
        return result;
    }
}
```

### Using Individual Repositories

```csharp
public class RegulationService
{
    private readonly IFishingRegulationRepository _regulationRepo;
    private readonly IWaterBodyRepository _waterBodyRepo;

    public RegulationService(
        IFishingRegulationRepository regulationRepo,
        IWaterBodyRepository waterBodyRepo)
    {
        _regulationRepo = regulationRepo;
        _waterBodyRepo = waterBodyRepo;
    }

    public async Task<IEnumerable<FishingRegulation>> GetCurrentRegulationsAsync(
        int waterBodyId, int speciesId)
    {
        return await _regulationRepo.GetByWaterBodyAndSpeciesAsync(waterBodyId, speciesId);
    }
}
```

### Transaction Support

```csharp
public async Task ImportRegulationsAsync(RegulationDocument document, 
    IEnumerable<FishingRegulation> regulations)
{
    using var transaction = await _unitOfWork.BeginTransactionAsync();
    try
    {
        var savedDoc = await _unitOfWork.RegulationDocuments.AddAsync(document);
        await _unitOfWork.SaveChangesAsync();

        foreach (var regulation in regulations)
        {
            regulation.SourceDocumentId = savedDoc.Id;
            await _unitOfWork.FishingRegulations.AddAsync(regulation);
        }

        await _unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

## Repository Methods

### Generic Repository (IRepository<T>)
- `GetByIdAsync(id)` - Get entity by primary key
- `GetAllAsync()` - Get all entities
- `FindAsync(predicate)` - Find entities by condition
- `AddAsync(entity)` - Add new entity
- `Update(entity)` - Update existing entity
- `Remove(entity)` - Delete entity
- `CountAsync(predicate)` - Count entities
- `AnyAsync(predicate)` - Check if any entities match

### Water Body Repository (IWaterBodyRepository)
- `GetByStateAsync(stateId)` - Get water bodies by state
- `GetByCountyAsync(countyId)` - Get water bodies by county
- `GetByTypeAsync(waterType)` - Get by water type (lake, river, etc.)
- `SearchByNameAsync(pattern)` - Search by name pattern
- `GetByGeographicAreaAsync(bounds)` - Get by geographic coordinates
- `GetWithRelatedDataAsync()` - Get with related entities loaded

### Fishing Regulation Repository (IFishingRegulationRepository)
- `GetByWaterBodyAsync(waterBodyId)` - Get regulations for water body
- `GetByFishSpeciesAsync(speciesId)` - Get regulations for fish species
- `GetActiveRegulationsAsync()` - Get currently active regulations
- `GetByEffectiveDateAsync(date)` - Get regulations effective on date
- `GetSeasonalRegulationsAsync()` - Get regulations with seasons
- `GetWithLimitsAsync()` - Get regulations with size/bag limits

### Regulation Document Repository (IRegulationDocumentRepository)
- `GetByStateAsync(stateId)` - Get documents by state
- `GetByStatusAsync(status)` - Get by processing status
- `GetProcessedDocumentsAsync()` - Get successfully processed documents
- `GetPendingProcessingAsync()` - Get documents awaiting processing
- `GetFailedDocumentsAsync()` - Get documents with processing errors

## Database Schema

The data access layer supports the following main entities:

- **States** - US states and provinces
- **Counties** - Counties within states
- **FishSpecies** - Fish species lookup table
- **WaterBodies** - Lakes, rivers, streams with geographic data
- **FishingRegulations** - Species-specific regulations per water body
- **RegulationDocuments** - PDF documents containing regulations
- **Users** - Application users with preferences
- **Analytics** - Search history and audit logging

## Migrations

To create a new migration:

```bash
dotnet ef migrations add MigrationName --project FishingRegs.Data
```

To update database:

```bash
dotnet ef database update --project FishingRegs.Data
```

## Health Checks

The data access layer includes health checks for monitoring database connectivity:

```csharp
// In Program.cs
app.MapHealthChecks("/health");
```

## Dependencies

- Microsoft.EntityFrameworkCore (8.0.8)
- Npgsql.EntityFrameworkCore.PostgreSQL (8.0.4)
- Microsoft.Extensions.DependencyInjection.Abstractions (8.0.1)
- Microsoft.Extensions.Configuration.Binder (8.0.2)
- Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore (8.0.8)

## License

This project is part of the Fishing Regulations application.

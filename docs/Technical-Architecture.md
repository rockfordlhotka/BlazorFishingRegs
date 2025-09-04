# Fishing Regulations Technical Architecture Document

## 1. System Architecture Overview

This document provides detailed technical architecture for the Blazor AI Fishing Regulations application, focusing on the extraction and presentation of lake-specific fishing regulations from text documents. The application is designed to run in Docker containers with Docker Compose for local development and container orchestration for production deployment.

## 2. Container Architecture

### 2.1 Containerization Strategy

The application follows a microservices-oriented containerization approach with the following containers:

```
┌─────────────────────────────────────────────────────────────┐
│                    Docker Compose Environment                │
├─────────────────┬─────────────────┬─────────────────────────┤
│   Web App       │   Database      │     Supporting          │
│   Container     │   Container     │     Services            │
├─────────────────┼─────────────────┼─────────────────────────┤
│ • Blazor Server │ • SQL Server    │ • Azurite (Storage)     │
│ • .NET 8        │ • Entity        │ • Seq (Logging)         │
│ • Kestrel       │   Framework     │ • Seq (Logging)         │
│                 │                 │ • NGINX (Reverse Proxy) │
└─────────────────┴─────────────────┴─────────────────────────┘
```

### 2.2 Container Services

#### 2.2.1 Application Container (blazor-fishing-app)
- **Base Image**: `mcr.microsoft.com/dotnet/aspnet:8.0`
- **Purpose**: Main Blazor Server application
- **Ports**: 8080 (HTTP), 8443 (HTTPS)
- **Volumes**: Configuration, data, logs
- **Dependencies**: SQL Server, Azurite

#### 2.2.2 Database Container (sql-server)
- **Base Image**: `mcr.microsoft.com/mssql/server:2022-latest`
- **Purpose**: SQL Server database for lakes and regulations
- **Ports**: 1433
- **Volumes**: Database data persistence
- **Environment**: Development settings

#### 2.2.3 Storage Container (azurite)
- **Base Image**: `mcr.microsoft.com/azure-storage/azurite`
- **Purpose**: Local Azure Storage emulation for documents (optional)
- **Ports**: 10000 (Blob), 10001 (Queue), 10002 (Table)
- **Volumes**: Storage data persistence

#### 2.2.4 Logging Container (seq)
- **Base Image**: `datalust/seq:latest`
- **Purpose**: Centralized logging and monitoring
- **Ports**: 5341 (ingestion), 80 (UI)
- **Volumes**: Log data persistence

#### 2.2.5 Reverse Proxy Container (nginx)
- **Base Image**: `nginx:alpine`
- **Purpose**: Load balancing and SSL termination
- **Ports**: 80 (HTTP), 443 (HTTPS)
- **Configuration**: Custom nginx.conf for routing

## 3. Application Layers

### 3.1 Presentation Layer (Blazor Server)

#### 3.1.1 Page Components
```
/Pages
├── Index.razor              # Main dashboard with lake search
├── LakeSelection.razor      # Interactive lake selection interface
├── RegulationView.razor     # Lake-specific regulation display  
├── Upload.razor             # Regulation text file upload (admin)
├── Processing.razor         # Text file processing status
└── Admin/
    ├── RegulationManagement.razor  # Regulation oversight
    └── LakeManagement.razor        # Lake database management
```

#### 3.1.2 Shared Components
```
/Shared
├── MainLayout.razor         # Application layout
├── LakeMap.razor           # Interactive lake map component
├── LakeSelector.razor      # Lake dropdown/search component
├── RegulationCard.razor    # Individual regulation display
├── SeasonCalendar.razor    # Fishing season visualization
├── SpeciesFilter.razor     # Fish species filtering
└── RegulationUpload.razor  # Text file upload component
```

#### 3.1.3 Component Services
```csharp
// Blazor component services registration in Program.cs
builder.Services.AddScoped<ILakeSelectionService, LakeSelectionService>();
builder.Services.AddScoped<IRegulationDisplayService, RegulationDisplayService>();
builder.Services.AddScoped<IMapService, MapService>();
builder.Services.AddScoped<IRegulationUploadService, RegulationUploadService>();

// Container-specific configurations
builder.Services.AddDbContext<FishingRegulationsDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});
```

### 3.2 Business Logic Layer

#### 3.2.1 Core Services
```csharp
// IRegulationProcessingService.cs
public interface IRegulationProcessingService
{
    Task<RegulationProcessingResult> ProcessRegulationDocumentAsync(Guid documentId, CancellationToken cancellationToken);
    Task<List<LakeRegulation>> ExtractLakeRegulationsAsync(Stream documentStream);
    Task<ValidationResult> ValidateExtractedRegulationsAsync(List<LakeRegulation> regulations);
}

// ILakeService.cs
public interface ILakeService
{
    Task<Lake> GetLakeByIdAsync(Guid lakeId);
    Task<List<Lake>> SearchLakesAsync(string searchTerm, string state = null);
    Task<List<Lake>> GetLakesByRegionAsync(decimal latitude, decimal longitude, double radiusMiles);
    Task<List<FishingRegulation>> GetRegulationsForLakeAsync(Guid lakeId, string species = null);
}

// IRegulationSearchService.cs
public interface IRegulationSearchService
{
    Task<List<FishingRegulation>> SearchRegulationsAsync(RegulationSearchCriteria criteria);
    Task<List<Lake>> FindLakesBySpeciesAsync(string species);
    Task<SeasonInfo> GetFishingSeasonAsync(Guid lakeId, string species, DateTime date);
}
```

#### 2.2.2 Service Implementations
```csharp
// RegulationProcessingService.cs
public class RegulationProcessingService : IRegulationProcessingService
{
    private readonly IAIRegulationExtractionService _aiService;
    private readonly ILakeService _lakeService;
    private readonly IRegulationRepository _regulationRepo;
    private readonly ILogger<RegulationProcessingService> _logger;

    public async Task<RegulationProcessingResult> ProcessRegulationDocumentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await _regulationRepo.GetDocumentByIdAsync(documentId);
        
        // Step 1: AI Analysis of Fishing Regulations
        var extractedRegulations = await _aiService.ExtractLakeRegulationsAsync(document.BlobUrl);
        
        // Step 2: Lake Identification and Matching
        var matchedLakes = await IdentifyAndMatchLakesAsync(extractedRegulations);
        
        // Step 3: Regulation Structuring and Validation
        var structuredRegulations = await StructureRegulationDataAsync(extractedRegulations, matchedLakes);
        
        // Step 4: Database Update
        await UpdateLakeRegulationsAsync(structuredRegulations);
        
        return new RegulationProcessingResult
        {
            DocumentId = documentId,
            ProcessedLakeCount = matchedLakes.Count,
            ExtractedRegulationCount = structuredRegulations.Count,
            ProcessedAt = DateTime.UtcNow
        };
    }

    private async Task<List<Lake>> IdentifyAndMatchLakesAsync(List<ExtractedRegulation> regulations)
    {
        var identifiedLakes = new List<Lake>();
        
        foreach (var regulation in regulations)
        {
            // Use AI to standardize lake names and identify coordinates
            var lakeInfo = await _aiService.StandardizeLakeInformationAsync(regulation.LakeName, regulation.Location);
            
            // Match with existing lakes or create new entries
            var existingLake = await _lakeService.FindLakeByNameAndLocationAsync(lakeInfo.StandardizedName, lakeInfo.State);
            
            if (existingLake == null)
            {
                existingLake = await _lakeService.CreateLakeAsync(lakeInfo);
            }
            
            identifiedLakes.Add(existingLake);
        }
        
        return identifiedLakes;
    }
}
```

### 2.3 Data Access Layer

#### 2.3.1 Entity Framework Configuration
```csharp
// FishingRegulationsDbContext.cs
public class FishingRegulationsDbContext : DbContext
{
    public DbSet<Lake> Lakes { get; set; }
    public DbSet<FishingRegulation> FishingRegulations { get; set; }
    public DbSet<RegulationDocument> RegulationDocuments { get; set; }
    public DbSet<ProcessingAudit> ProcessingAudits { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Lake configuration
        modelBuilder.Entity<Lake>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.State).IsRequired().HasMaxLength(50);
            entity.Property(e => e.County).HasMaxLength(50);
            entity.Property(e => e.Latitude).HasPrecision(10, 7);
            entity.Property(e => e.Longitude).HasPrecision(10, 7);
            entity.HasIndex(e => new { e.State, e.Name });
            entity.HasIndex(e => new { e.Latitude, e.Longitude });
        });

        // Fishing Regulation configuration
        modelBuilder.Entity<FishingRegulation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Species).IsRequired().HasMaxLength(50);
            entity.Property(e => e.MinimumSize).HasMaxLength(20);
            entity.Property(e => e.MaximumSize).HasMaxLength(20);
            entity.Property(e => e.ProtectedSlot).HasMaxLength(50);
            entity.Property(e => e.ConfidenceScore).HasPrecision(5, 4);
            entity.HasOne<Lake>().WithMany(l => l.Regulations).HasForeignKey(e => e.LakeId);
            entity.HasIndex(e => new { e.LakeId, e.Species });
        });

        // Regulation Document configuration
        modelBuilder.Entity<RegulationDocument>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.RegulationYear).HasMaxLength(4);
            entity.Property(e => e.IssuingAuthority).HasMaxLength(100);
            entity.HasIndex(e => e.RegulationYear);
            entity.HasIndex(e => e.UploadedAt);
        });
    }
}
```

#### 2.3.2 Repository Pattern Implementation
```csharp
// ILakeRepository.cs
public interface ILakeRepository
{
    Task<Lake> GetByIdAsync(Guid id);
    Task<Lake> GetByNameAndStateAsync(string name, string state);
    Task<List<Lake>> SearchAsync(string searchTerm, string state = null);
    Task<List<Lake>> GetByRegionAsync(decimal latitude, decimal longitude, double radiusMiles);
    Task<Lake> CreateAsync(Lake lake);
    Task<Lake> UpdateAsync(Lake lake);
    Task<List<Lake>> GetLakesBySpeciesAsync(string species);
}

// LakeRepository.cs
public class LakeRepository : ILakeRepository
{
    private readonly FishingRegulationsDbContext _context;

    public async Task<Lake> GetByIdAsync(Guid id)
    {
        return await _context.Lakes
            .Include(l => l.Regulations)
            .FirstOrDefaultAsync(l => l.Id == id);
    }

    public async Task<List<Lake>> SearchAsync(string searchTerm, string state = null)
    {
        var query = _context.Lakes.AsQueryable();

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(l => l.Name.Contains(searchTerm) || 
                                   l.County.Contains(searchTerm));
        }

        if (!string.IsNullOrEmpty(state))
        {
            query = query.Where(l => l.State == state);
        }

        return await query
            .OrderBy(l => l.Name)
            .Take(50)
            .ToListAsync();
    }

    public async Task<List<Lake>> GetByRegionAsync(decimal latitude, decimal longitude, double radiusMiles)
    {
        // Using geographic distance calculation
        var latRange = radiusMiles / 69.0; // Approximate miles per degree latitude
        var lonRange = radiusMiles / (69.0 * Math.Cos((double)latitude * Math.PI / 180.0));

        return await _context.Lakes
            .Where(l => Math.Abs((double)(l.Latitude - latitude)) <= latRange &&
                       Math.Abs((double)(l.Longitude - longitude)) <= lonRange)
            .OrderBy(l => 
                Math.Sqrt(Math.Pow((double)(l.Latitude - latitude), 2) + 
                         Math.Pow((double)(l.Longitude - longitude), 2)))
            .ToListAsync();
    }
}
```

## 3. AI Integration Architecture

### 3.1 Azure AI Document Intelligence Integration

#### 3.1.1 Service Configuration
```csharp
// AIDocumentIntelligenceClient.cs
public class AIDocumentIntelligenceClient : IAIDocumentIntelligenceClient
{
    private readonly DocumentAnalysisClient _client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AIDocumentIntelligenceClient> _logger;

    public AIDocumentIntelligenceClient(IConfiguration configuration, ILogger<AIDocumentIntelligenceClient> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        var endpoint = configuration["AzureAI:DocumentIntelligence:Endpoint"];
        var apiKey = configuration["AzureAI:DocumentIntelligence:ApiKey"];
        
        _client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
    }

    public async Task<DocumentAnalysisResult> AnalyzeDocumentAsync(string documentUrl, string modelId = "prebuilt-document")
    {
        try
        {
            var operation = await _client.AnalyzeDocumentFromUriAsync(WaitUntil.Completed, modelId, new Uri(documentUrl));
            var result = operation.Value;

            return new DocumentAnalysisResult
            {
                DocumentType = DetermineDocumentType(result),
                ExtractedFields = ExtractKeyValuePairs(result),
                Tables = ExtractTables(result),
                ConfidenceScores = CalculateConfidenceScores(result),
                ProcessedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze document from URL: {DocumentUrl}", documentUrl);
            throw;
        }
    }

    private Dictionary<string, ExtractedField> ExtractKeyValuePairs(AnalyzeResult result)
    {
        var fields = new Dictionary<string, ExtractedField>();

        foreach (var document in result.Documents)
        {
            foreach (var field in document.Fields)
            {
                fields[field.Key] = new ExtractedField
                {
                    Name = field.Key,
                    Value = field.Value.Content,
                    Confidence = field.Value.Confidence ?? 0,
                    BoundingBox = ExtractBoundingBox(field.Value),
                    FieldType = MapFieldType(field.Value.FieldType)
                };
            }
        }

        return fields;
    }
}
```

### 3.2 Azure OpenAI Integration

#### 3.2.1 Enhanced Processing Service
```csharp
// AzureOpenAIService.cs
public class AzureOpenAIService : IAzureOpenAIService
{
    private readonly OpenAIClient _client;
    private readonly IConfiguration _configuration;

    public async Task<EnhancedExtractionResult> EnhanceExtractionAsync(DocumentAnalysisResult analysisResult)
    {
        var prompt = BuildEnhancementPrompt(analysisResult);
        
        var completionOptions = new ChatCompletionsOptions
        {
            DeploymentName = _configuration["AzureOpenAI:DeploymentName"],
            Messages =
            {
                new ChatRequestSystemMessage("You are an expert at analyzing document data and improving field extraction accuracy."),
                new ChatRequestUserMessage(prompt)
            },
            Temperature = 0.1f,
            MaxTokens = 2000
        };

        var response = await _client.GetChatCompletionsAsync(completionOptions);
        var enhancedData = ParseEnhancementResponse(response.Value.Choices[0].Message.Content);

        return new EnhancedExtractionResult
        {
            OriginalResult = analysisResult,
            EnhancedFields = enhancedData.Fields,
            DocumentClassification = enhancedData.Classification,
            QualityScore = enhancedData.QualityScore,
            Suggestions = enhancedData.Suggestions
        };
    }

    private string BuildEnhancementPrompt(DocumentAnalysisResult result)
    {
        return $@"
Please analyze the following extracted document data and provide enhanced, normalized field values:

Document Type: {result.DocumentType}
Extracted Fields: {JsonSerializer.Serialize(result.ExtractedFields, new JsonSerializerOptions { WriteIndented = true })}

Please provide:
1. Normalized field values (proper formatting, standardized formats)
2. Document classification confidence
3. Quality assessment (0-100)
4. Suggestions for improving extraction

Response format: JSON with fields: enhancedFields, classification, qualityScore, suggestions";
    }
}
```

## 4. File Storage Architecture

### 4.1 Azure Blob Storage Configuration

#### 4.1.1 Blob Storage Service
```csharp
// IBlobStorageService.cs
public interface IBlobStorageService
{
    Task<BlobUploadResult> UploadDocumentAsync(Stream stream, string fileName, string contentType);
    Task<Stream> DownloadDocumentAsync(string blobName);
    Task<bool> DeleteDocumentAsync(string blobName);
    Task<string> GeneratePresignedUrlAsync(string blobName, TimeSpan expiry);
}

// BlobStorageService.cs
public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobContainerClient _containerClient;
    private readonly IConfiguration _configuration;

    public BlobStorageService(IConfiguration configuration)
    {
        _configuration = configuration;
        var connectionString = configuration.GetConnectionString("AzureStorage");
        var containerName = configuration["AzureStorage:ContainerName"];

        _blobServiceClient = new BlobServiceClient(connectionString);
        _containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
    }

    public async Task<BlobUploadResult> UploadDocumentAsync(Stream stream, string fileName, string contentType)
    {
        var blobName = GenerateUniqueBlobName(fileName);
        var blobClient = _containerClient.GetBlobClient(blobName);

        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            Metadata = new Dictionary<string, string>
            {
                ["OriginalFileName"] = fileName,
                ["UploadedAt"] = DateTime.UtcNow.ToString("O")
            }
        };

        await blobClient.UploadAsync(stream, uploadOptions);

        return new BlobUploadResult
        {
            BlobName = blobName,
            BlobUrl = blobClient.Uri.ToString(),
            ContentType = contentType,
            Size = stream.Length
        };
    }

    private string GenerateUniqueBlobName(string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName);
        var uniqueId = Guid.NewGuid().ToString("N");
        return $"{DateTime.UtcNow:yyyy/MM/dd}/{uniqueId}{extension}";
    }
}
```

## 5. Security Implementation

### 5.1 Authentication and Authorization

#### 5.1.1 Azure AD B2C Configuration
```csharp
// Startup.cs Authentication Configuration
public void ConfigureServices(IServiceCollection services)
{
    services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(options =>
        {
            Configuration.Bind("AzureAdB2C", options);
            options.Events = new OpenIdConnectEvents
            {
                OnRedirectToIdentityProvider = context =>
                {
                    context.ProtocolMessage.SetParameter("ui_locales", "en-US");
                    return Task.CompletedTask;
                }
            };
        });

    services.AddAuthorization(options =>
    {
        options.AddPolicy("DocumentProcessor", policy =>
            policy.RequireClaim("extension_Role", "DocumentProcessor", "Administrator"));
        
        options.AddPolicy("Administrator", policy =>
            policy.RequireClaim("extension_Role", "Administrator"));
    });
}
```

#### 5.1.2 Custom Authorization Handlers
```csharp
// DocumentAccessHandler.cs
public class DocumentAccessHandler : AuthorizationHandler<DocumentAccessRequirement, Document>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DocumentAccessRequirement requirement,
        Document resource)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (resource.UploadedBy == userId || 
            context.User.HasClaim("extension_Role", "Administrator"))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
```

### 5.2 Data Protection and Encryption

#### 5.2.1 Sensitive Data Handling
```csharp
// IDataProtectionService.cs
public interface IDataProtectionService
{
    string EncryptSensitiveData(string data);
    string DecryptSensitiveData(string encryptedData);
    string HashPII(string personalData);
    bool VerifyHash(string data, string hash);
}

// DataProtectionService.cs
public class DataProtectionService : IDataProtectionService
{
    private readonly IDataProtector _protector;

    public DataProtectionService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("BlazorAI.SensitiveData");
    }

    public string EncryptSensitiveData(string data)
    {
        return _protector.Protect(data);
    }

    public string DecryptSensitiveData(string encryptedData)
    {
        return _protector.Unprotect(encryptedData);
    }
}
```

## 6. Performance Optimization

### 6.2 Database Optimization

#### 6.1.1 Hosted Service for Document Processing
```csharp
// DocumentProcessingHostedService.cs
public class DocumentProcessingHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DocumentProcessingHostedService> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessPendingDocuments(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task ProcessPendingDocuments(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var documentService = scope.ServiceProvider.GetRequiredService<IDocumentProcessingService>();
        var documentRepo = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();

        var pendingDocuments = await documentRepo.GetByStatusAsync(DocumentStatus.Pending);

        var tasks = pendingDocuments.Select(async doc =>
        {
            try
            {
                await documentService.ProcessDocumentAsync(doc.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process document {DocumentId}", doc.Id);
            }
        });

        await Task.WhenAll(tasks);
    }
}
```

## 7. Monitoring and Logging

### 7.1 Application Insights Integration

#### 7.1.1 Custom Telemetry
```csharp
// ITelemetryService.cs
public interface ITelemetryService
{
    void TrackDocumentProcessed(Guid documentId, TimeSpan processingTime, bool success);
    void TrackAIExtractionAccuracy(string documentType, double accuracy);
    void TrackUserAction(string action, Dictionary<string, string> properties);
}

// ApplicationInsightsTelemetryService.cs
public class ApplicationInsightsTelemetryService : ITelemetryService
{
    private readonly TelemetryClient _telemetryClient;

    public void TrackDocumentProcessed(Guid documentId, TimeSpan processingTime, bool success)
    {
        var properties = new Dictionary<string, string>
        {
            ["DocumentId"] = documentId.ToString(),
            ["Success"] = success.ToString()
        };

        var metrics = new Dictionary<string, double>
        {
            ["ProcessingTimeMs"] = processingTime.TotalMilliseconds
        };

        _telemetryClient.TrackEvent("DocumentProcessed", properties, metrics);
    }
}
```

## 8. API Design

### 8.1 REST API Endpoints

#### 8.1.1 Document Management API
```csharp
// DocumentsController.cs
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentProcessingService _processingService;
    private readonly IDocumentRepository _documentRepository;

    [HttpPost("upload")]
    public async Task<ActionResult<DocumentUploadResponse>> UploadDocument([FromForm] DocumentUploadRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _processingService.UploadDocumentAsync(request.File, request.DocumentType);
        
        return Ok(new DocumentUploadResponse
        {
            DocumentId = result.DocumentId,
            Status = result.Status,
            Message = "Document uploaded successfully"
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DocumentDetailResponse>> GetDocument(Guid id)
    {
        var document = await _documentRepository.GetByIdAsync(id);
        if (document == null)
            return NotFound();

        return Ok(new DocumentDetailResponse
        {
            Id = document.Id,
            FileName = document.FileName,
            Status = document.Status,
            ExtractedData = document.ExtractedData,
            ProcessedAt = document.ProcessedAt
        });
    }

    [HttpPost("{id}/process")]
    public async Task<ActionResult> ProcessDocument(Guid id)
    {
        await _processingService.ProcessDocumentAsync(id, CancellationToken.None);
        return Accepted();
    }
}
```

## 9. Configuration Management

### 9.1 Application Settings Structure

#### 9.1.1 appsettings.json Structure
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=BlazorAI;Trusted_Connection=true;MultipleActiveResultSets=true",
    "AzureStorage": "DefaultEndpointsProtocol=https;AccountName={account};AccountKey={key};EndpointSuffix=core.windows.net"
  },
  "AzureAI": {
    "DocumentIntelligence": {
      "Endpoint": "https://{resource}.cognitiveservices.azure.com/",
      "ApiKey": "{api-key}"
    },
    "OpenAI": {
      "Endpoint": "https://{resource}.openai.azure.com/",
      "ApiKey": "{api-key}",
      "DeploymentName": "gpt-4"
    }
  },
  "AzureStorage": {
    "ContainerName": "documents"
  },
  "Processing": {
    "MaxConcurrentDocuments": 10,
    "ProcessingTimeoutMinutes": 30,
    "RetryAttempts": 3
  },
  "Security": {
    "RequireHttps": true,
    "DataRetentionDays": 365,
    "EnableAuditLogging": true
  }
}
```

---

This technical architecture document provides the detailed implementation guidance needed to build the Blazor AI Fishing Regulations application according to the specifications outlined in the main document.

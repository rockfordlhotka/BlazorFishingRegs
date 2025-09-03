# Technical Architecture Document

## 1. System Architecture Overview

This document provides detailed technical architecture for the Blazor AI PDF Form Population application, complementing the main specification document.

## 2. Application Layers

### 2.1 Presentation Layer (Blazor Server)

#### 2.1.1 Page Components
```
/Pages
├── Index.razor              # Main dashboard
├── Upload.razor             # Document upload page
├── Processing.razor         # Processing status page
├── FormEdit.razor          # Form editing and validation
├── Results.razor           # Results display and export
└── Admin/
    ├── Templates.razor     # Form template management
    └── Settings.razor      # Application settings
```

#### 2.1.2 Shared Components
```
/Shared
├── MainLayout.razor        # Application layout
├── NavMenu.razor          # Navigation component
├── FileUpload.razor       # Reusable file upload
├── FormRenderer.razor     # Dynamic form renderer
├── ProgressIndicator.razor # Processing progress
├── DataGrid.razor         # Data display grid
└── ConfidenceIndicator.razor # AI confidence display
```

#### 2.1.3 Component Services
```csharp
// Blazor component services registration
builder.Services.AddScoped<IDocumentUploadService, DocumentUploadService>();
builder.Services.AddScoped<IFormRenderingService, FormRenderingService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IProgressTrackingService, ProgressTrackingService>();
```

### 2.2 Business Logic Layer

#### 2.2.1 Core Services
```csharp
// IDocumentProcessingService.cs
public interface IDocumentProcessingService
{
    Task<ProcessingResult> ProcessDocumentAsync(Guid documentId, CancellationToken cancellationToken);
    Task<ExtractionResult> ExtractDataAsync(Stream documentStream, string documentType);
    Task<ValidationResult> ValidateExtractedDataAsync(ExtractionResult extraction);
}

// IFormConfigurationService.cs
public interface IFormConfigurationService
{
    Task<FormTemplate> GetTemplateAsync(string documentType);
    Task<FormTemplate> CreateTemplateAsync(FormTemplateRequest request);
    Task<FieldMapping[]> GetFieldMappingsAsync(string templateId);
}

// IAIOrchestrationService.cs
public interface IAIOrchestrationService
{
    Task<DocumentAnalysisResult> AnalyzeDocumentAsync(string blobUrl);
    Task<EnhancedExtractionResult> EnhanceExtractionAsync(DocumentAnalysisResult result);
    Task<FieldSuggestion[]> GetFieldSuggestionsAsync(string context);
}
```

#### 2.2.2 Service Implementations
```csharp
// DocumentProcessingService.cs
public class DocumentProcessingService : IDocumentProcessingService
{
    private readonly IAIOrchestrationService _aiService;
    private readonly IFormConfigurationService _formService;
    private readonly IDocumentRepository _documentRepo;
    private readonly ILogger<DocumentProcessingService> _logger;

    public async Task<ProcessingResult> ProcessDocumentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await _documentRepo.GetByIdAsync(documentId);
        
        // Step 1: AI Analysis
        var analysisResult = await _aiService.AnalyzeDocumentAsync(document.BlobUrl);
        
        // Step 2: Enhanced Processing
        var enhancedResult = await _aiService.EnhanceExtractionAsync(analysisResult);
        
        // Step 3: Form Template Selection
        var template = await _formService.GetTemplateAsync(enhancedResult.DocumentType);
        
        // Step 4: Field Mapping
        var mappedData = MapFieldsToTemplate(enhancedResult, template);
        
        // Step 5: Validation
        var validationResult = await ValidateExtractedDataAsync(mappedData);
        
        return new ProcessingResult
        {
            DocumentId = documentId,
            ExtractedData = mappedData,
            ValidationResult = validationResult,
            ProcessedAt = DateTime.UtcNow
        };
    }
}
```

### 2.3 Data Access Layer

#### 2.3.1 Entity Framework Configuration
```csharp
// ApplicationDbContext.cs
public class ApplicationDbContext : DbContext
{
    public DbSet<Document> Documents { get; set; }
    public DbSet<FormTemplate> FormTemplates { get; set; }
    public DbSet<ExtractedData> ExtractedData { get; set; }
    public DbSet<ProcessingAudit> ProcessingAudits { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Document configuration
        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.BlobUrl).IsRequired();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.UploadedAt);
        });

        // Form Template configuration
        modelBuilder.Entity<FormTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TemplateJson).IsRequired();
            entity.HasMany(e => e.Fields).WithOne().HasForeignKey("TemplateId");
        });

        // Extracted Data configuration
        modelBuilder.Entity<ExtractedData>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FieldName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ConfidenceScore).HasPrecision(5, 4);
            entity.HasOne<Document>().WithMany(d => d.ExtractedData).HasForeignKey(e => e.DocumentId);
        });
    }
}
```

#### 2.3.2 Repository Pattern Implementation
```csharp
// IDocumentRepository.cs
public interface IDocumentRepository
{
    Task<Document> GetByIdAsync(Guid id);
    Task<Document> CreateAsync(Document document);
    Task<Document> UpdateAsync(Document document);
    Task<PagedResult<Document>> GetPagedAsync(DocumentFilter filter, int page, int pageSize);
    Task<IEnumerable<Document>> GetByStatusAsync(DocumentStatus status);
}

// DocumentRepository.cs
public class DocumentRepository : IDocumentRepository
{
    private readonly ApplicationDbContext _context;

    public async Task<Document> GetByIdAsync(Guid id)
    {
        return await _context.Documents
            .Include(d => d.ExtractedData)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<PagedResult<Document>> GetPagedAsync(DocumentFilter filter, int page, int pageSize)
    {
        var query = _context.Documents.AsQueryable();

        if (filter.Status.HasValue)
            query = query.Where(d => d.Status == filter.Status.Value);

        if (filter.FromDate.HasValue)
            query = query.Where(d => d.UploadedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(d => d.UploadedAt <= filter.ToDate.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(d => d.UploadedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Document>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
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

### 6.1 Caching Strategy

#### 6.1.1 Distributed Caching Implementation
```csharp
// ICacheService.cs
public interface ICacheService
{
    Task<T> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class;
    Task RemoveAsync(string key);
    Task RemovePatternAsync(string pattern);
}

// RedisCacheService.cs
public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisCacheService> _logger;

    public async Task<T> GetAsync<T>(string key) where T : class
    {
        try
        {
            var cachedValue = await _cache.GetStringAsync(key);
            return cachedValue == null ? null : JsonSerializer.Deserialize<T>(cachedValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cached value for key: {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
    {
        var options = new DistributedCacheEntryOptions();
        if (expiry.HasValue)
            options.SetAbsoluteExpiration(expiry.Value);

        var serializedValue = JsonSerializer.Serialize(value);
        await _cache.SetStringAsync(key, serializedValue, options);
    }
}
```

### 6.2 Background Processing

#### 6.2.1 Hosted Service for Document Processing
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

This technical architecture document provides the detailed implementation guidance needed to build the Blazor AI PDF Form Population application according to the specifications outlined in the main document.

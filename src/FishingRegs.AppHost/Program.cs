using Aspire.Hosting;

// Set required environment variables for Aspire dashboard development
Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "https://localhost:15888;http://localhost:15889");
Environment.SetEnvironmentVariable("DOTNET_DASHBOARD_OTLP_ENDPOINT_URL", "https://localhost:16001");
Environment.SetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true");

var builder = DistributedApplication.CreateBuilder(args);

// For development without Docker, comment out containerized services
// Uncomment when Docker Desktop is available and you want to use containers

// Add PostgreSQL (supports both local and Azure PostgreSQL)
// For local development, uses containerized PostgreSQL
// For Azure, configure connection string in user secrets or app settings
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("fishing-regs-postgres-data")
    .WithEnvironment("POSTGRES_DB", "FishingRegsDB");

// var database = postgres.AddDatabase("FishingRegsDB");

// Add Redis Cache
var redis = builder.AddRedis("redis")
    .WithDataVolume("fishing-regs-redis-data");

// Add Azure Storage (using Azurite for local development)
var storage = builder.AddAzureStorage("storage");
var blobs = storage.AddBlobs("blobs");

// Add Seq for logging
var seq = builder.AddSeq("seq")
    .WithDataVolume("fishing-regs-seq-data");

// TODO: Add AI Mock Service when project is created
// var aiMockService = builder.AddProject<Projects.FishingRegs_AIMockService>("ai-mock-service")
//     .WithHttpEndpoint(port: 7000, name: "http");

// TODO: Add main Blazor application when project is created
// var blazorApp = builder.AddProject<Projects.BlazorFishingRegs>("blazor-app")
//     .WithReference(database)
//     .WithReference(redis)
//     .WithReference(blobs)
//     .WithReference(aiMockService)
//     .WithEnvironment("Seq__ServerUrl", seq.GetEndpoint("http"))
//     .WithHttpsEndpoint(port: 8443, name: "https")
//     .WithHttpEndpoint(port: 8080, name: "http");

var app = builder.Build();

await app.RunAsync();

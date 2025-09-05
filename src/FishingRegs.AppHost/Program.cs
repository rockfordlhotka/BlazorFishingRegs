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

var database = postgres.AddDatabase("FishingRegsDB");

// Note: Azure Storage is configured via user secrets in the Web project
// using ConnectionStrings:AzureStorage rather than Aspire hosting integration

// Add Seq for logging
var seq = builder.AddSeq("seq")
    .WithDataVolume("fishing-regs-seq-data");

// TODO: Add AI Mock Service when project is created
// var aiMockService = builder.AddProject<Projects.FishingRegs_AIMockService>("ai-mock-service")
//     .WithHttpEndpoint(port: 7000, name: "http");

// Add main Blazor Web application
var blazorApp = builder.AddProject<Projects.FishingRegs_Web>("fishing-regs-web")
    .WithReference(database)
    .WithEnvironment("Seq__ServerUrl", seq.GetEndpoint("http"));

var app = builder.Build();

await app.RunAsync();

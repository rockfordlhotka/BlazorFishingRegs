var builder = DistributedApplication.CreateBuilder(args);

// Add SQL Server
var sqlServer = builder.AddSqlServer("sql-server")
    .WithDataVolume("fishing-regs-sql-data")
    .WithLifetime(ContainerLifetime.Persistent);

var database = sqlServer.AddDatabase("FishingRegsDB");

// Add Redis Cache
var redis = builder.AddRedis("redis")
    .WithDataVolume("fishing-regs-redis-data")
    .WithLifetime(ContainerLifetime.Persistent);

// Add Azure Storage (using Azurite for local development)
var storage = builder.AddAzureStorage("storage");
var blobs = storage.AddBlobs("blobs");

// Add Seq for logging
var seq = builder.AddSeq("seq")
    .WithDataVolume("fishing-regs-seq-data")
    .WithLifetime(ContainerLifetime.Persistent);

// Add AI Mock Service
var aiMockService = builder.AddProject<Projects.FishingRegs_AIMockService>("ai-mock-service")
    .WithHttpEndpoint(port: 7000, name: "http");

// Add main Blazor application
var blazorApp = builder.AddProject<Projects.BlazorFishingRegs>("blazor-app")
    .WithReference(database)
    .WithReference(redis)
    .WithReference(blobs)
    .WithReference(aiMockService)
    .WithEnvironment("Seq__ServerUrl", seq.GetEndpoint("http"))
    .WithHttpsEndpoint(port: 8443, name: "https")
    .WithHttpEndpoint(port: 8080, name: "http");

// Add NGINX reverse proxy (optional - Aspire has built-in ingress)
if (builder.Environment.IsDevelopment())
{
    // In development, expose services directly
    blazorApp.WithExternalHttpEndpoints();
}

builder.Build().Run();

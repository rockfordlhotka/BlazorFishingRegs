using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Add SQL Server
var sqlServer = builder.AddSqlServer("sql-server")
    .WithDataVolume("fishing-regs-sql-data");

var database = sqlServer.AddDatabase("FishingRegsDB");

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

using Microsoft.Azure.Cosmos;
using Azure.Storage.Blobs;
using Valuation.Api.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);


// 2) Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Valuation API",
        Version = "v1"
    });
});

// 3) Load & validate Cosmos settings
var cosmosCfg = builder.Configuration.GetSection("Cosmos");
var accountEndpoint = cosmosCfg["AccountEndpoint"];
var accountKey = cosmosCfg["AccountKey"];
if (string.IsNullOrWhiteSpace(accountEndpoint) || string.IsNullOrWhiteSpace(accountKey))
{
    throw new InvalidOperationException(
        "Missing Cosmos configuration. Ensure Cosmos:AccountEndpoint and Cosmos:AccountKey are set.");
}

// 4) Create CosmosClient & provision container
var cosmosClient = new CosmosClient(accountEndpoint, accountKey);
var databaseId = cosmosCfg["DatabaseId"] ?? "ValuationsDb";
var containerId = cosmosCfg["ContainerId"] ?? "Valuations";

var databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(id: databaseId);
var containerProps = new ContainerProperties
{
    Id = containerId,
    PartitionKeyPath = "/CompositeKey"
};
await databaseResponse.Database.CreateContainerIfNotExistsAsync(containerProps);

// 5) Register CosmosClient
builder.Services.AddSingleton(_ => cosmosClient);

// 6) Blob storage clients
var blobConn = builder.Configuration["Blob:ConnectionString"]
                    ?? throw new InvalidOperationException("Missing Blob:ConnectionString");
var blobContainer = builder.Configuration["Blob:ContainerName"]
                    ?? throw new InvalidOperationException("Missing Blob:ContainerName");

builder.Services.AddSingleton(_ => new BlobServiceClient(blobConn));
builder.Services.AddSingleton(_ => new BlobContainerClient(blobConn, blobContainer));

// 1) Add MVC controllers
builder.Services.AddControllers();

// 7) Application services
builder.Services.AddScoped<IStakeholderService, StakeholderService>();

var app = builder.Build();

// 8) Middleware pipeline
app.UseRouting();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Valuation API V1");
    c.RoutePrefix = string.Empty;
});

// (Optional) app.UseAuthorization();

app.MapControllers();

app.Run();

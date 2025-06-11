using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Text;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Valuation.Api.Services;
using Valuation.Api.Repositories;

var builder = WebApplication.CreateBuilder(args);

// --- SET QUESTPDF LICENSE HERE ---
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
QuestPDF.Settings.EnableDebugging = true;

// 1) Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Valuation API", Version = "v1" });
});

// 2) HTTP Clients
builder.Services.AddHttpClient();

// 3) Load & validate Cosmos settings
var cosmosCfg = builder.Configuration.GetSection("Cosmos");
var accountEndpoint = cosmosCfg["AccountEndpoint"];
var accountKey = cosmosCfg["AccountKey"];
if (string.IsNullOrWhiteSpace(accountEndpoint) || string.IsNullOrWhiteSpace(accountKey))
{
    throw new InvalidOperationException(
        "Missing Cosmos configuration. Ensure Cosmos:AccountEndpoint and Cosmos:AccountKey are set.");
}

// 4) Initialize Cosmos DB and provision container
var cosmosOptions = new CosmosClientOptions { ConnectionMode = ConnectionMode.Direct };
var cosmosClient = new CosmosClient(accountEndpoint, accountKey, cosmosOptions);
var databaseId = cosmosCfg["DatabaseId"] ?? "ValuationsDb";
var containerId = cosmosCfg["ContainerId"] ?? "Valuations";

var dbResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
await dbResponse.Database.CreateContainerIfNotExistsAsync(new ContainerProperties
{
    Id = containerId,
    PartitionKeyPath = "/CompositeKey"
});

builder.Services.AddSingleton(_ => cosmosClient);

// 5) Azure Table Storage client
var tableConn = builder.Configuration.GetConnectionString("TableStorage")
               ?? throw new InvalidOperationException("TableStorage connection string not configured.");
builder.Services.AddSingleton(_ => new TableServiceClient(tableConn));

// 6) Blob Storage clients
var blobConn = builder.Configuration["Blob:ConnectionString"]
               ?? throw new InvalidOperationException("Missing Blob:ConnectionString");
var blobContainer = builder.Configuration["Blob:ContainerName"]
                    ?? throw new InvalidOperationException("Missing Blob:ContainerName");

builder.Services.AddSingleton(_ => new BlobServiceClient(blobConn));
builder.Services.AddSingleton(_ => new BlobContainerClient(blobConn, blobContainer));

// 7) OpenAI and Google CSE HTTP clients
builder.Services.AddHttpClient("OpenAI", client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/");
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", builder.Configuration["OpenAI:ApiKey"]);
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddHttpClient("GoogleCSE", client =>
{
    client.BaseAddress = new Uri("https://www.googleapis.com/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

// 8) JWT Bearer Authentication (custom OTP tokens)
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIss = builder.Configuration["Jwt:Issuer"];
var jwtAud = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
  .AddJwtBearer(options =>
  {
      options.TokenValidationParameters = new TokenValidationParameters
      {
          ValidateIssuerSigningKey = true,
          IssuerSigningKey = new SymmetricSecurityKey(
                 Encoding.UTF8.GetBytes(jwtKey)),
          ValidateIssuer = true,
          ValidIssuer = jwtIss,
          ValidateAudience = true,
          ValidAudience = jwtAud,
          ValidateLifetime = true,
          ClockSkew = TimeSpan.Zero
      };
  });

builder.Services.AddAuthorization();

// 9) Application services
builder.Services.AddScoped<IStakeholderService, StakeholderService>();
builder.Services.AddScoped<IValuationService, ValuationService>();
builder.Services.AddScoped<IGetInspectionService, GetInspectionService>();
builder.Services.AddScoped<IQualityControlService, QualityControlService>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddTransient<IChatGptRepository, ChatGptRepository>();
builder.Services.AddTransient<IVehicleValuationService, VehicleValuationService>();
builder.Services.AddScoped<IVehiclePhotoService, VehiclePhotoService>();
builder.Services.AddScoped<IValuationResponseService, ValuationResponseService>();
builder.Services.AddScoped<IFinalReportPdfService, FinalReportPdfService>();
builder.Services.AddScoped<IWorkflowTableService, WorkflowTableService>();
builder.Services.AddScoped<IOtpService, OtpService>();

// 10) MVC controllers
builder.Services.AddControllers();

var app = builder.Build();

// 11) Middleware pipeline
app.UseRouting();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Valuation API V1");
    c.RoutePrefix = string.Empty;
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

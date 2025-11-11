using System.Net.Http.Headers;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using Microsoft.OpenApi.Models;
using Valuation.Api.Repositories;
using Valuation.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// --- 0) QuestPDF license & debugging ---
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
QuestPDF.Settings.EnableDebugging = true;

// --- 0.5) ✅ ADD CORS CONFIGURATION HERE (BEFORE builder.Build())
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins(
            "http://localhost:4200",      // Angular dev server
            "http://localhost:3000",      // Alternative port
            "http://127.0.0.1:4200"       // Localhost alias
        )
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
});

// --- 1) Swagger/OpenAPI ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Valuation API", Version = "v1" });
});

// --- 2) Named HttpClients ---
builder.Services.AddHttpClient();
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

// --- 3) Cosmos DB setup ---
var cosmosCfg  = builder.Configuration.GetSection("Cosmos");
var cosmosEndp = cosmosCfg["AccountEndpoint"];
var cosmosKey  = cosmosCfg["AccountKey"];
if (string.IsNullOrWhiteSpace(cosmosEndp) || string.IsNullOrWhiteSpace(cosmosKey))
    throw new InvalidOperationException("Missing Cosmos configuration.");

var cosmosClient = new CosmosClient(
    cosmosEndp,
    cosmosKey,
    new CosmosClientOptions { ConnectionMode = ConnectionMode.Direct });

var dbId   = cosmosCfg["DatabaseId"]  ?? "ValuationsDb";
var contId = cosmosCfg["ContainerId"] ?? "Valuations";

var dbRes = await cosmosClient.CreateDatabaseIfNotExistsAsync(dbId);
await dbRes.Database.CreateContainerIfNotExistsAsync(new ContainerProperties
{
    Id               = contId,
    PartitionKeyPath = "/CompositeKey"
});

builder.Services.AddSingleton(_ => cosmosClient);

// --- 4) Table Storage: Workflow (prontovalutions) + States (prontoblobs) tables ---
var workflowConn   = builder.Configuration.GetConnectionString("TableStorage")
                         ?? throw new InvalidOperationException("TableStorage connection missing.");
var workflowTable  = builder.Configuration["TableStorage:TableName"] ?? "Workflow";

var blobConn       = builder.Configuration["Blob:ConnectionString"]
                         ?? throw new InvalidOperationException("Missing Blob:ConnectionString");
var statesTable    = builder.Configuration.GetConnectionString("StatesTableName")
                         ?? throw new InvalidOperationException("StatesTableName missing.");

var workflowTableSvc = new TableServiceClient(workflowConn);
await workflowTableSvc.CreateTableIfNotExistsAsync(workflowTable);
builder.Services.AddSingleton(workflowTableSvc);
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<TableServiceClient>()
      .GetTableClient(workflowTable));

var statesTableSvc = new TableServiceClient(blobConn);
await statesTableSvc.CreateTableIfNotExistsAsync(statesTable);
builder.Services.AddSingleton(statesTableSvc);
builder.Services.AddScoped<IStateService>(sp =>
    new StatesService(
        sp.GetRequiredService<TableServiceClient>()
          .GetTableClient(statesTable)
    ));

// --- 5) Blob Storage clients ---
var blobContainer = builder.Configuration["Blob:ContainerName"]
                         ?? throw new InvalidOperationException("Missing Blob:ContainerName");

builder.Services.AddSingleton(_ => new BlobServiceClient(blobConn));
builder.Services.AddSingleton(_ => new BlobContainerClient(blobConn, blobContainer));

// --- 6) Other app services & repositories ---
builder.Services.AddScoped<IStakeholderService, StakeholderService>();
builder.Services.AddScoped<IValuationService, ValuationService>();
builder.Services.AddScoped<IGetInspectionService, GetInspectionService>();
builder.Services.AddScoped<IQualityControlService, QualityControlService>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<IWorkflowTableService, WorkflowTableService>();

builder.Services.AddHttpClient(nameof(PincodeTableService));
builder.Services.AddSingleton<IPincodeTableService, PincodeTableService>();

builder.Services.AddTransient<IChatGptRepository, ChatGptRepository>();
builder.Services.AddTransient<IVehicleValuationService, VehicleValuationService>();
builder.Services.AddScoped<IVehiclePhotoService, VehiclePhotoService>();
builder.Services.AddScoped<IValuationResponseService, ValuationResponseService>();
builder.Services.AddScoped<IFinalReportPdfService, FinalReportPdfService>();

builder.Services.AddScoped<IRoleService, TableRoleService>();
builder.Services.AddScoped<TableRoleService>();

// ✅ CommonNoteService is already registered
builder.Services.AddScoped<ICommonNoteService, CommonNoteService>();

// --- 7) MVC Controllers ---
builder.Services.AddControllers();

var app = builder.Build();

// --- 8) Middleware pipeline ---
// ✅ ADD CORS MIDDLEWARE HERE (BEFORE UseRouting)
app.UseCors("AllowAngular");

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

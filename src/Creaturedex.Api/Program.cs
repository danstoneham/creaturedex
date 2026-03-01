using Microsoft.Extensions.AI;
using OllamaSharp;
using Serilog;
using Creaturedex.AI;
using Creaturedex.AI.Services;
using Creaturedex.Api.Services;
using Creaturedex.Data;
using Creaturedex.Data.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration));

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=Creaturedex;Trusted_Connection=True;";
builder.Services.AddSingleton(new DbConnectionFactory(connectionString));

// Repositories
builder.Services.AddScoped<AnimalRepository>();
builder.Services.AddScoped<CategoryRepository>();
builder.Services.AddScoped<TaxonomyRepository>();
builder.Services.AddScoped<PetCareGuideRepository>();
builder.Services.AddScoped<CharacteristicRepository>();
builder.Services.AddScoped<TagRepository>();
builder.Services.AddScoped<EmbeddingRepository>();
builder.Services.AddScoped<SearchRepository>();

// AI
var aiConfig = builder.Configuration.GetSection("AI").Get<AIConfig>() ?? new AIConfig();
builder.Services.AddSingleton(aiConfig);

var ollamaClient = new OllamaApiClient(new Uri(aiConfig.OllamaEndpoint));
ollamaClient.SelectedModel = aiConfig.ChatModel;
builder.Services.AddSingleton<IChatClient>(ollamaClient);

var ollamaEmbedder = new OllamaApiClient(new Uri(aiConfig.OllamaEndpoint));
ollamaEmbedder.SelectedModel = aiConfig.EmbeddingModel;
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(ollamaEmbedder);

builder.Services.AddHttpClient<ImageGenerationService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5); // Image generation can be slow
});
builder.Services.AddScoped<AIService>();
builder.Services.AddScoped<EmbeddingService>();
builder.Services.AddScoped<ContentGeneratorService>();
builder.Services.AddScoped<SemanticSearchService>();
builder.Services.AddScoped<MatcherAIService>();

// Services
builder.Services.AddScoped<AnimalService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<SearchService>();
builder.Services.AddScoped<MatcherService>();
builder.Services.AddScoped<ContentGenerationService>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// TODO: DACPAC deploy disabled — .NET 10 SDK compatibility issues
// var dacpacPath = Path.Combine(AppContext.BaseDirectory, "Creaturedex.Database.dacpac");
// DacpacDeployer.Deploy(connectionString, dacpacPath);

// Run seed migrations
if (!MigrationRunner.Run(connectionString))
{
    Console.Error.WriteLine("Database seed migration failed!");
    return;
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// Ensure image storage directory exists
var imageStoragePath = Path.Combine(AppContext.BaseDirectory, aiConfig.ImageStoragePath);
Directory.CreateDirectory(imageStoragePath);

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseCors();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(AppContext.BaseDirectory, "wwwroot")),
    RequestPath = ""
});
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapGet("/health/db", async (DbConnectionFactory dbFactory) =>
{
    try
    {
        using var conn = dbFactory.CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1";
        cmd.ExecuteScalar();
        return Results.Ok(new { status = "healthy", database = "connected" });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "unhealthy", database = "disconnected", error = ex.Message },
            statusCode: 503);
    }
});

app.MapGet("/health/ai", async (AIConfig aiCfg) =>
{
    try
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var response = await http.GetAsync($"{aiCfg.OllamaEndpoint}/api/tags");
        response.EnsureSuccessStatusCode();
        return Results.Ok(new { status = "healthy", endpoint = aiCfg.OllamaEndpoint });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "unhealthy", endpoint = aiCfg.OllamaEndpoint, error = ex.Message },
            statusCode: 503);
    }
});

app.Run();

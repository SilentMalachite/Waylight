using LocalLlmAssistant.Data;
using LocalLlmAssistant.Models;
using LocalLlmAssistant.Services.Chain;
using LocalLlmAssistant.Services.Embeddings;
using LocalLlmAssistant.Services.Llm;
using LocalLlmAssistant.Services.Rag;
using LocalLlmAssistant.Services.Tools;
using LocalLlmAssistant.SSE;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ロギング設定
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// JSON設定
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

// 設定
builder.Services.Configure<RagConfig>(builder.Configuration.GetSection("Rag"));
builder.Services.Configure<LlmConfig>(builder.Configuration.GetSection("Llm"));
builder.Services.Configure<ChainConfig>(builder.Configuration.GetSection("Chain"));

// データベース
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=App_Data/app.db";
    opt.UseSqlite(connectionString);

    // 開発環境では詳細なエラーを有効化
    if (builder.Environment.IsDevelopment())
    {
        opt.EnableSensitiveDataLogging();
        opt.EnableDetailedErrors();
    }
});

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Waylight API",
        Version = "v1",
        Description = "Local LLM Assistant API with RAG and Tool execution"
    });
});

// HTTPクライアント
builder.Services.AddHttpClient()
    .ConfigureHttpClientDefaults(http =>
    {
        // デフォルトのHTTPクライアント設定
        http.ConfigureHttpClient(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        });
    });

// サービス登録
builder.Services.AddSingleton<StreamBroker>();
builder.Services.AddSingleton<EmbeddingsClient>();
builder.Services.AddScoped<RagRetriever>();
builder.Services.AddScoped<ToolRegistry>();
builder.Services.AddScoped<ToolRunner>();
builder.Services.AddScoped<LocalLlmAssistant.Services.HistoryCompressor>();
builder.Services.AddScoped<LocalLlmAssistant.Services.Chain.ChainService>();
builder.Services.AddScoped<LlmClientResolver>();

// OllamaClient と LmStudioClient は LlmClientResolver で動的に作成されるため、
// 直接登録しない（ActivatorUtilities.CreateInstance を使用）

// コントローラー
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

var app = builder.Build();

// データベース初期化
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migration completed successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during database migration");
    }
}

// 静的ファイルの提供を有効化
app.UseDefaultFiles();
app.UseStaticFiles();

// CORS設定（必要に応じて）
if (app.Environment.IsDevelopment())
{
    app.UseCors(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
}

// Swagger（開発環境のみ）
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ルーティング
app.MapControllers();

// ヘルスチェックエンドポイント
app.MapGet("/healthz", async (AppDbContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        return Results.Json(new { status = canConnect ? "healthy" : "unhealthy", timestamp = DateTime.UtcNow });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "error", error = ex.Message, timestamp = DateTime.UtcNow });
    }
});

app.Run();

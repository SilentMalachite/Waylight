using LocalLlmAssistant.Data;
using LocalLlmAssistant.Models;
using LocalLlmAssistant.Services.Embeddings;
using LocalLlmAssistant.Services.Llm;
using LocalLlmAssistant.Services.Rag;
using LocalLlmAssistant.Services.Tools;
using LocalLlmAssistant.SSE;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.Configure<RagConfig>(builder.Configuration.GetSection("Rag"));
builder.Services.Configure<LlmConfig>(builder.Configuration.GetSection("Llm"));

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=App_Data/app.db");
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<StreamBroker>();
builder.Services.AddSingleton<EmbeddingsClient>();
builder.Services.AddScoped<RagRetriever>();
builder.Services.AddScoped<ToolRegistry>();
builder.Services.AddScoped<ToolRunner>();
builder.Services.AddScoped<LocalLlmAssistant.Services.HistoryCompressor>();

builder.Services.AddScoped<LlmClientResolver>();
// OllamaClient と LmStudioClient は LlmClientResolver で動的に作成されるため、
// 直接登録しない

builder.Services.AddControllers();

var app = builder.Build();

// 静的ファイルの提供を有効化
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.MapGet("/healthz", async (AppDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();
    return Results.Json(new { status = canConnect ? "ok" : "ng" });
});

app.Run();

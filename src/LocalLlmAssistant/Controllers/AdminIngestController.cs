using LocalLlmAssistant.Data;
using LocalLlmAssistant.Models;
using LocalLlmAssistant.Services.Embeddings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LocalLlmAssistant.Controllers;

[ApiController]
[Route("api/admin/ingest")]
public class AdminIngestController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly EmbeddingsClient _emb;
    private readonly ILogger<AdminIngestController> _logger;

    public AdminIngestController(AppDbContext db, EmbeddingsClient emb, ILogger<AdminIngestController> logger) 
    { 
        _db = db; 
        _emb = emb; 
        _logger = logger;
    }

    public record IngestReq(string Text);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] IngestReq req)
    {
        if (string.IsNullOrWhiteSpace(req.Text)) return BadRequest(new { error = "text required" });

        var userId = HttpContext.User?.Identity?.Name ?? "guest";
        var doc = new Document { Title = $"ingested-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}", UserId = userId };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        // 簡易チャンク
        var parts = System.Text.RegularExpressions.Regex.Matches(req.Text, ".{1,800}", System.Text.RegularExpressions.RegexOptions.Singleline);
        var texts = parts.Select(m => m.Value).ToList();
        var vecs = await _emb.EmbedAsync(texts);

        for (int i = 0; i < texts.Count; i++)
        {
            _db.DocumentChunks.Add(new DocumentChunk { DocumentId = doc.Id, Text = texts[i], Embedding = JsonSerializer.Serialize(vecs[i]) });
        }
        await _db.SaveChangesAsync();

        // FTS へ投入（仮想テーブルに同期トリガがある場合は不要）
        try
        {
            await _db.Database.ExecuteSqlRawAsync("INSERT INTO document_chunks_fts(rowid, text) SELECT id, text FROM document_chunks WHERE document_id = {0}", doc.Id);
        } 
        catch (Exception ex) 
        { 
            // FTSテーブルが存在しない場合は無視（初期セットアップ時は正常）
            _logger.LogDebug(ex, "FTS insert failed (this is normal during initial setup)");
        }

        return Ok(new { status = "ok", chunks = texts.Count });
    }
}

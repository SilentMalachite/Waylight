using LocalLlmAssistant.Data;
using LocalLlmAssistant.Models;
using LocalLlmAssistant.Services.Embeddings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LocalLlmAssistant.Controllers;

[ApiController]
[Route("api/admin/ingest")]
public partial class AdminIngestController : ControllerBase
{
    [System.Text.RegularExpressions.GeneratedRegex(".{1,800}", System.Text.RegularExpressions.RegexOptions.Singleline)]
    private static partial System.Text.RegularExpressions.Regex ChunkRegex();

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
        if (string.IsNullOrWhiteSpace(req.Text))
        {
            return BadRequest(new { error = "text required" });
        }

        try
        {
            var userId = HttpContext.User?.Identity?.Name ?? "guest";
            var doc = new Document
            {
                Title = $"ingested-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
                UserId = userId
            };
            _db.Documents.Add(doc);
            await _db.SaveChangesAsync();

            // 簡易チャンク（正規表現の最適化）
            var chunkRegex = ChunkRegex();
            var parts = chunkRegex.Matches(req.Text);
            var texts = parts.Select(m => m.Value).ToList();

            if (texts.Count == 0)
            {
                return BadRequest(new { error = "No chunks created from text" });
            }

            _logger.LogInformation("Creating {Count} chunks for document {DocId}", texts.Count, doc.Id);

            var vecs = await _emb.EmbedAsync(texts);

            if (vecs.Count != texts.Count)
            {
                _logger.LogError("Embedding count mismatch: {VecCount} vs {TextCount}", vecs.Count, texts.Count);
                return StatusCode(500, new { error = "Embedding generation failed" });
            }

            for (int i = 0; i < texts.Count; i++)
            {
                _db.DocumentChunks.Add(new DocumentChunk
                {
                    DocumentId = doc.Id,
                    Text = texts[i],
                    Embedding = JsonSerializer.Serialize(vecs[i])
                });
            }
            await _db.SaveChangesAsync();

            // FTS へ投入（仮想テーブルに同期トリガがある場合は不要）
            try
            {
                await _db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO document_chunks_fts(rowid, text) SELECT id, text FROM document_chunks WHERE document_id = {0}",
                    doc.Id
                );
                _logger.LogInformation("Successfully inserted {Count} chunks into FTS for document {DocId}", texts.Count, doc.Id);
            }
            catch (Exception ex)
            {
                // FTSテーブルが存在しない場合は無視（初期セットアップ時は正常）
                _logger.LogWarning(ex, "FTS insert failed for document {DocId} (this is normal during initial setup)", doc.Id);
            }

            return Ok(new { status = "ok", documentId = doc.Id, chunks = texts.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting document");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }
}

using System.Text.Json;
using LocalLlmAssistant.Data;
using LocalLlmAssistant.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LocalLlmAssistant.Services.Rag;

public class RagRetriever
{
    private readonly AppDbContext _db;
    private readonly Embeddings.EmbeddingsClient _emb;
    private readonly RagConfig _config;
    private readonly ILogger<RagRetriever> _logger;

    public RagRetriever(AppDbContext db, Embeddings.EmbeddingsClient emb, IOptions<RagConfig> config, ILogger<RagRetriever> logger)
    {
        _db = db;
        _emb = emb;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<List<DocumentChunk>> SimilarChunksAsync(string query, int? k = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogDebug("Empty query provided to SimilarChunksAsync");
            return new List<DocumentChunk>();
        }

        var topK = k ?? _config.TopK;
        if (topK <= 0)
        {
            _logger.LogWarning("Invalid topK value: {TopK}", topK);
            return new List<DocumentChunk>();
        }

        try
        {
            var qvec = await _emb.EmbedAsync(query);

            if (qvec == null || qvec.Length == 0)
            {
                _logger.LogError("Failed to generate query embedding");
                return new List<DocumentChunk>();
            }

            // FTS 後候補取得（存在しない場合は直近で代替）
            List<int> ids;
            try
            {
                var rows = await _db.Database.SqlQueryRaw<int>(
                    "SELECT rowid FROM document_chunks_fts WHERE document_chunks_fts MATCH @query LIMIT @limit",
                    new SqliteParameter("@query", query),
                    new SqliteParameter("@limit", _config.FtsCandidates))
                    .ToListAsync();
                ids = rows;

                _logger.LogDebug("FTS returned {Count} candidates", ids.Count);
            }
            catch (Exception ex)
            {
                // FTSテーブルが存在しない場合は直近のチャンクを代替として使用
                _logger.LogWarning(ex, "FTS query failed, falling back to recent chunks");
                ids = await _db.DocumentChunks
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => c.Id)
                    .Take(_config.FtsCandidates)
                    .ToListAsync();
            }

            if (ids.Count == 0)
            {
                _logger.LogInformation("No document chunks found for query");
                return new List<DocumentChunk>();
            }

            var candidates = await _db.DocumentChunks.Where(c => ids.Contains(c.Id)).ToListAsync();

            // 埋め込みベクトルを持つ候補のみ抽出
            var candidatesWithEmbeddings = candidates
                .Select(c => (c, emb: string.IsNullOrWhiteSpace(c.Embedding)
                    ? Array.Empty<double>()
                    : JsonSerializer.Deserialize<double[]>(c.Embedding!) ?? Array.Empty<double>()))
                .Where(t => t.emb.Length > 0)
                .ToList();

            if (candidatesWithEmbeddings.Count == 0)
            {
                _logger.LogWarning("No candidates with valid embeddings found");
                return new List<DocumentChunk>();
            }

            // MMR で多様化しながら k 件選択
            var selected = MmrSelect(qvec, candidatesWithEmbeddings, topK, _config.MmrLambda);

            _logger.LogInformation("Selected {Count} chunks using MMR", selected.Count);

            return selected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SimilarChunksAsync");
            return new List<DocumentChunk>();
        }
    }

    /// <summary>
    /// Maximal Marginal Relevance (MMR) による多様化選択
    /// λ: 関連性と多様性のバランス (0.5 = バランス, 1.0 = 関連性重視, 0.0 = 多様性重視)
    /// </summary>
    private List<DocumentChunk> MmrSelect(
        double[] queryVec,
        List<(DocumentChunk c, double[] emb)> candidates,
        int topK,
        double lambda)
    {
        if (candidates.Count == 0) return new();
        if (candidates.Count <= topK) return candidates.Select(t => t.c).ToList();

        var selected = new List<(DocumentChunk c, double[] emb)>();
        var remaining = new List<(DocumentChunk c, double[] emb)>(candidates);

        // 1件目: クエリとの類似度が最も高いものを選択
        var first = remaining.OrderByDescending(t => VectorMath.Cosine(queryVec, t.emb)).First();
        selected.Add(first);
        remaining.Remove(first);

        // 2件目以降: MMR スコアが最も高いものを選択
        while (selected.Count < topK && remaining.Count > 0)
        {
            var best = remaining
                .Select(cand =>
                {
                    // クエリとの類似度
                    var relevance = VectorMath.Cosine(queryVec, cand.emb);

                    // 既選択チャンクとの最大類似度（多様性ペナルティ）
                    var maxSim = selected.Max(sel => VectorMath.Cosine(cand.emb, sel.emb));

                    // MMR スコア = λ * 関連性 - (1-λ) * 既選択との類似度
                    var mmrScore = lambda * relevance - (1 - lambda) * maxSim;

                    return (cand, mmrScore);
                })
                .OrderByDescending(t => t.mmrScore)
                .First();

            selected.Add(best.cand);
            remaining.Remove(best.cand);
        }

        return selected.Select(t => t.c).ToList();
    }
}

public static class RagFormatter
{
    public static string ToSystemContext(List<DocumentChunk> chunks)
    {
        if (chunks.Count == 0) return "";
        var header = "以下は内部コンテキストの抜粋です。事実に基づいて回答してください。\n\n";
        var body = string.Join("\n\n", chunks.Select((c, i) => $"[{i + 1}] {c.Text}"));
        return header + body;
    }
}

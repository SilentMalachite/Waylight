using LocalLlmAssistant.Models;

namespace LocalLlmAssistant.Services;

public class HistoryCompressor
{
    private const int MAX_TOKENS_THRESHOLD = 8000;
    private const int TARGET_TOKENS = 4000;
    private readonly ILogger<HistoryCompressor> _logger;

    public HistoryCompressor(ILogger<HistoryCompressor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 履歴圧縮: トークン数が閾値を超えた場合、古いメッセージを要約または削除
    /// </summary>
    public List<Message> CompressIfNeeded(List<Message> messages)
    {
        if (messages == null || messages.Count == 0)
        {
            return new List<Message>();
        }

        var totalTokens = messages.Sum(m => m.TokenCount);

        if (totalTokens <= MAX_TOKENS_THRESHOLD)
        {
            _logger.LogDebug("Token count {Total} is within threshold {Threshold}", totalTokens, MAX_TOKENS_THRESHOLD);
            return messages;
        }

        _logger.LogInformation("Compressing history: {Total} tokens exceeds threshold {Threshold}", totalTokens, MAX_TOKENS_THRESHOLD);

        // システムメッセージは保持
        var systemMessages = messages.Where(m => m.Role == "system").ToList();
        var nonSystemMessages = messages.Where(m => m.Role != "system").ToList();

        // 最新のメッセージから逆順に取得し、TARGET_TOKENS に収まるまで追加
        var compressed = new List<Message>();
        var currentTokens = systemMessages.Sum(m => m.TokenCount);

        foreach (var msg in nonSystemMessages.AsEnumerable().Reverse())
        {
            if (currentTokens + msg.TokenCount <= TARGET_TOKENS)
            {
                compressed.Insert(0, msg);
                currentTokens += msg.TokenCount;
            }
            else
            {
                break;
            }
        }

        _logger.LogInformation("Compressed to {Count} messages with {Tokens} tokens",
            systemMessages.Count + compressed.Count, currentTokens);

        // システムメッセージを先頭に配置
        return systemMessages.Concat(compressed).ToList();
    }

    /// <summary>
    /// 履歴を要約して1つのメッセージにまとめる（将来実装）
    /// </summary>
    public Message SummarizeHistory(List<Message> messages)
    {
        if (messages == null || messages.Count == 0)
        {
            return new Message
            {
                Role = "system",
                Content = "過去の会話なし",
                TokenCount = 10
            };
        }

        // TODO: LLM を使って履歴を要約
        var summary = "過去の会話を要約: " + string.Join("; ", messages
            .Where(m => !string.IsNullOrEmpty(m.Content))
            .Select(m => m.Content!.Length > 50
                ? m.Content[..50] + "..."
                : m.Content));

        _logger.LogDebug("Generated summary with length {Length}", summary.Length);

        return new Message
        {
            Role = "system",
            Content = summary,
            TokenCount = EstimateTokenCount(summary)
        };
    }

    private static int EstimateTokenCount(string text)
    {
        return string.IsNullOrEmpty(text) ? 0 : text.Length / 4;
    }
}

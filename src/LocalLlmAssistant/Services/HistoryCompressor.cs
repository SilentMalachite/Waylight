using LocalLlmAssistant.Models;

namespace LocalLlmAssistant.Services;

public class HistoryCompressor
{
    private const int MAX_TOKENS_THRESHOLD = 8000;
    private const int TARGET_TOKENS = 4000;

    /// <summary>
    /// 履歴圧縮: トークン数が閾値を超えた場合、古いメッセージを要約または削除
    /// </summary>
    public List<Message> CompressIfNeeded(List<Message> messages)
    {
        var totalTokens = messages.Sum(m => m.TokenCount);
        if (totalTokens <= MAX_TOKENS_THRESHOLD)
        {
            return messages;
        }

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

        // システムメッセージを先頭に配置
        return systemMessages.Concat(compressed).ToList();
    }

    /// <summary>
    /// 履歴を要約して1つのメッセージにまとめる（将来実装）
    /// </summary>
    public Message SummarizeHistory(List<Message> messages)
    {
        // TODO: LLM を使って履歴を要約
        var summary = "過去の会話を要約: " + string.Join("; ", messages.Select(m => 
            m.Content != null && m.Content.Length > 0 
                ? m.Content.Substring(0, Math.Min(50, m.Content.Length)) 
                : ""));
        
        return new Message
        {
            Role = "system",
            Content = summary,
            TokenCount = summary.Length / 4 // 概算
        };
    }
}

using System.Collections.Concurrent;
using System.Threading.Channels;

namespace LocalLlmAssistant.SSE;

public record StreamEvent(string Event, string Data, string? TraceId = null);

public class StreamBroker
{
    private readonly ConcurrentDictionary<string, Channel<StreamEvent>> _channels = new();

    public ChannelReader<StreamEvent> Subscribe(string userKey)
    {
        var ch = _channels.GetOrAdd(userKey, _ => Channel.CreateUnbounded<StreamEvent>());
        return ch.Reader;
    }

    public async Task PublishAsync(string userKey, string evt, string data, string? traceId = null)
    {
        if (_channels.TryGetValue(userKey, out var ch))
        {
            await ch.Writer.WriteAsync(new StreamEvent(evt, data, traceId));
        }
    }

    public void Close(string userKey)
    {
        if (_channels.TryGetValue(userKey, out var ch))
        {
            ch.Writer.TryComplete();
            _channels.TryRemove(userKey, out _);
        }
    }
}

namespace SmsTwin.Inbound;

/// <summary>The incoming messages, in the order received.</summary>
internal sealed class InboundStore
{
    private readonly Lock gate = new();
    private readonly List<IncomingMessage> messages = [];

    public IncomingMessage Add(string from, string to, string body)
    {
        var message = new IncomingMessage($"in_{Guid.NewGuid():N}", from, to, body, DateTimeOffset.UtcNow);
        lock (gate)
        {
            messages.Add(message);
        }

        return message;
    }

    public IncomingMessage? Find(string id)
    {
        lock (gate)
        {
            return messages.FirstOrDefault(m => m.Id == id);
        }
    }

    public List<IncomingMessageView> List()
    {
        lock (gate)
        {
            return messages.Select(m => m.View()).ToList();
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            messages.Clear();
        }
    }
}

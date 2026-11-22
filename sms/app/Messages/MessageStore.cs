using SmsTwin.Sms;

namespace SmsTwin.Messages;

/// <summary>The stored messages, in the order received, and the idempotency keys they were stored under.</summary>
internal sealed class MessageStore
{
    private readonly Lock gate = new();
    private readonly List<Message> messages = [];
    private readonly Dictionary<string, Message> byKey = [];

    /// <summary>Stores the message, or finds the first one stored under the same idempotency key.</summary>
    public AcceptResult Accept(OutgoingMessage send)
    {
        lock (gate)
        {
            if (send.IdempotencyKey is { } key && byKey.TryGetValue(key, out var first))
            {
                return first.IsSameSend(send)
                    ? new AcceptResult(AcceptOutcome.Replayed, first)
                    : new AcceptResult(AcceptOutcome.Conflict, null);
            }

            var count = GsmEncoding.Count(send.Body);
            var message = new Message(
                $"sms_{Guid.NewGuid():N}", send.To, send.From, send.Body, count.Encoding, count.Segments, DateTimeOffset.UtcNow, send.IdempotencyKey);
            messages.Add(message);
            if (send.IdempotencyKey is { } newKey)
            {
                byKey[newKey] = message;
            }

            return new AcceptResult(AcceptOutcome.Stored, message);
        }
    }

    public List<Message> List(string? to)
    {
        lock (gate)
        {
            return messages.Where(m => to is null || m.To == to).ToList();
        }
    }

    /// <summary>Clears the messages and the idempotency keys they were stored under.</summary>
    public void Clear()
    {
        lock (gate)
        {
            messages.Clear();
            byKey.Clear();
        }
    }
}

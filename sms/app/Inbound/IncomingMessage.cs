using System.Text.Json;

namespace SmsTwin.Inbound;

/// <summary>
/// A simulated incoming SMS. <see cref="Payload"/> holds the exact bytes delivered,
/// so a redelivery sends the same bytes and the same signature.
/// </summary>
internal sealed class IncomingMessage
{
    private readonly Lock gate = new();
    private readonly List<DeliveryAttempt> attempts = [];
    private readonly WebhookPayload content;

    public IncomingMessage(string id, string from, string to, string body, DateTimeOffset receivedAt)
    {
        content = new WebhookPayload(id, from, to, body, receivedAt);
        Payload = JsonSerializer.SerializeToUtf8Bytes(content, JsonSerializerOptions.Web);
    }

    public string Id => content.Id;

    public byte[] Payload { get; }

    public void Record(DeliveryAttempt attempt)
    {
        lock (gate)
        {
            attempts.Add(attempt);
        }
    }

    public IncomingMessageView View()
    {
        lock (gate)
        {
            return new IncomingMessageView(content.Id, content.From, content.To, content.Body, content.ReceivedAt, [.. attempts]);
        }
    }
}

namespace SmsTwin.Messages;

/// <summary>A stored message, as <c>GET /twin/messages</c> lists it.</summary>
internal sealed record Message(
    string Id,
    string To,
    string From,
    string Body,
    string Encoding,
    int Segments,
    DateTimeOffset ReceivedAt,
    string? IdempotencyKey)
{
    /// <summary>True when a later request with this message's key asks for the same send.</summary>
    public bool IsSameSend(OutgoingMessage send) => send.To == To && send.From == From && send.Body == Body;
}

namespace SmsTwin.SelfTest.Client;

/// <summary>A stored message, as <c>GET /twin/messages</c> lists it.</summary>
internal sealed record SmsMessage(
    string Id,
    string To,
    string From,
    string Body,
    string Encoding,
    int Segments,
    DateTimeOffset ReceivedAt,
    string? IdempotencyKey);

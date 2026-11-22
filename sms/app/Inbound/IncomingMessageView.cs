namespace SmsTwin.Inbound;

/// <summary>An incoming message with its delivery attempts, as the test API shows it.</summary>
internal sealed record IncomingMessageView(
    string Id,
    string From,
    string To,
    string Body,
    DateTimeOffset ReceivedAt,
    IReadOnlyList<DeliveryAttempt> Attempts);

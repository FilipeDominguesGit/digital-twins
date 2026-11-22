namespace SmsTwin.SelfTest.Client;

/// <summary>An incoming message with its delivery attempts, as the twin shows it.</summary>
internal sealed record InboundView(
    string Id,
    string From,
    string To,
    string Body,
    DateTimeOffset ReceivedAt,
    IReadOnlyList<AttemptView> Attempts);

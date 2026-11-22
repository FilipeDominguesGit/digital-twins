namespace SmsTwin.Messages;

/// <summary>A valid send request, with its idempotency key when it had one.</summary>
internal sealed record OutgoingMessage(string To, string From, string Body, string? IdempotencyKey);

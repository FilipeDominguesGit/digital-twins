namespace SmsTwin.Inbound;

/// <summary>The JSON body delivered to the webhook. Its property order is the order of the keys.</summary>
internal sealed record WebhookPayload(string Id, string From, string To, string Body, DateTimeOffset ReceivedAt);

namespace SmsTwin.SelfTest.Webhook;

/// <summary>One request the twin made to the webhook: the exact body bytes, the signature header, the content type and the path.</summary>
internal sealed record WebhookDelivery(byte[] Body, string? Signature, string? ContentType, string Path);

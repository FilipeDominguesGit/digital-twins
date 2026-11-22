namespace SmsTwin.Inbound;

/// <summary>One delivery to the webhook: its HTTP status, or <c>null</c> with an error.</summary>
internal sealed record DeliveryAttempt(DateTimeOffset At, int? Status, string? Error);

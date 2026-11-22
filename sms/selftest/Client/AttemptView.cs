namespace SmsTwin.SelfTest.Client;

/// <summary>One delivery attempt: the webhook HTTP status, or an error.</summary>
internal sealed record AttemptView(DateTimeOffset At, int? Status, string? Error);

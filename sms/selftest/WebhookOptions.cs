namespace SmsTwin.SelfTest;

/// <summary>The secret the twin signs with, and the prefix the self-test listens on (the twin posts to its path /hook).</summary>
internal sealed record WebhookOptions(string Secret, string ListenPrefix);

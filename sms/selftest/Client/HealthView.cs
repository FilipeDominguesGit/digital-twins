namespace SmsTwin.SelfTest.Client;

/// <summary>The answer of <c>GET /health</c>.</summary>
internal sealed record HealthView(string Status, string Version);

namespace SmsTwin.Health;

/// <summary>The answer of <c>GET /health</c>.</summary>
internal sealed record HealthResponse(string Status, string Version);

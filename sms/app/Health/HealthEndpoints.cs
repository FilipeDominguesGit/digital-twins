namespace SmsTwin.Health;

internal static class HealthEndpoints
{
    public static void MapHealth(this IEndpointRouteBuilder app) =>
        app.MapGet("/health", () => Results.Json(new HealthResponse("ok", TwinVersion.Current)));
}

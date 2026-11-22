using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmsTwin.Http;

/// <summary>The options request records are read with: camelCase, case-sensitive, numbers only as JSON numbers.</summary>
internal static class StrictJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        NumberHandling = JsonNumberHandling.Strict,
        AllowDuplicateProperties = false,
    };
}

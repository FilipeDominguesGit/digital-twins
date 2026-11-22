using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SmsTwin.SelfTest.Client;

/// <summary>A twin's answer: the status and the raw text, readable as JSON or as a typed record.</summary>
internal sealed record TwinResponse(HttpStatusCode Status, string Text)
{
    public JsonNode? Json => Text.Length > 0 ? JsonNode.Parse(Text) : null;

    public T As<T>() =>
        JsonSerializer.Deserialize<T>(Text, JsonSerializerOptions.Web)
        ?? throw new InvalidOperationException($"expected {typeof(T).Name}, got {this}");

    public override string ToString() => $"{(int)Status} {Text}";
}

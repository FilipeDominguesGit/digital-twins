using System.Text.Json;
using System.Text.Json.Nodes;

namespace SmsTwin.SelfTest.Checking;

/// <summary>Helpers for the checks that are about the JSON itself: which keys, which value kinds.</summary>
internal static class JsonShape
{
    /// <summary>True when the node is an object with exactly these keys, in any order.</summary>
    public static bool HasExactly(JsonNode? node, params string[] keys) =>
        node is JsonObject o && o.Count == keys.Length && keys.All(o.ContainsKey);

    public static bool IsString(JsonNode? node) => node?.GetValueKind() == JsonValueKind.String;

    public static bool IsNumber(JsonNode? node) => node?.GetValueKind() == JsonValueKind.Number;

    /// <summary>True when the body is the error shape: <c>{ "error": "&lt;non-empty text&gt;" }</c>.</summary>
    public static bool IsError(JsonNode? node) =>
        HasExactly(node, "error") && IsString(node!["error"]) && node["error"]!.GetValue<string>().Length > 0;
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmsTwin.Http;

/// <summary>
/// Reads and writes an enum as its kebab-case name (<c>AcceptThenError</c> is <c>"accept-then-error"</c>).
/// Reading is exact: no other capitals, no numbers, no member names, no comma lists.
/// </summary>
internal sealed class KebabCaseEnumConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(Converter<>).MakeGenericType(typeToConvert))!;

    private sealed class Converter<TEnum> : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        private static readonly Dictionary<string, TEnum> ByName =
            Enum.GetValues<TEnum>().ToDictionary(NameOf, StringComparer.Ordinal);

        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String && reader.GetString() is { } name && ByName.TryGetValue(name, out var value))
            {
                return value;
            }

            throw new JsonException($"expected one of {string.Join(", ", ByName.Keys)}");
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
            writer.WriteStringValue(NameOf(value));

        private static string NameOf(TEnum value) => JsonNamingPolicy.KebabCaseLower.ConvertName(value.ToString());
    }
}

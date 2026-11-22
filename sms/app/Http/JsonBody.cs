using System.Text.Json;

namespace SmsTwin.Http;

/// <summary>
/// Reads a request body into a request record, strictly: it must be valid UTF-8 JSON, one object,
/// with no duplicate keys, and every known field of its JSON type. Keys are case-sensitive and unknown
/// keys are ignored. Any other body gives the 400 <c>{ "error": ... }</c> answer.
/// The endpoints never use the minimal API's automatic body binding, which answers differently.
/// </summary>
internal static class JsonBody
{
    public const string NotJson = "the body is not valid JSON";
    public const string NotAnObject = "the body must be a JSON object";

    private static readonly JsonDocumentOptions DocumentOptions = new() { AllowDuplicateProperties = false };

    public static async Task<(T? Value, IResult? Error)> Read<T>(HttpRequest request)
        where T : class, IJsonRequest
    {
        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(request.Body, DocumentOptions, request.HttpContext.RequestAborted);
        }
        catch (JsonException)
        {
            return (null, ApiError.BadRequest(NotJson));
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (null, ApiError.BadRequest(NotAnObject));
            }

            try
            {
                return (document.RootElement.Deserialize<T>(StrictJson.Options), null);
            }
            catch (JsonException e)
            {
                return (null, ApiError.BadRequest(T.ProblemFor(FieldName(e.Path))));
            }
            catch (InvalidOperationException)
            {
                // A string that is not valid UTF-8, or a lone surrogate escape such as "\ud800".
                return (null, ApiError.BadRequest(NotJson));
            }
        }
    }

    // "$.times" -> "times"
    private static string FieldName(string? path) =>
        path is not null && path.StartsWith("$.", StringComparison.Ordinal) ? path[2..].Split('.', '[')[0] : string.Empty;
}

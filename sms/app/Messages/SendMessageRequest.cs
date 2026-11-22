using SmsTwin.Http;

namespace SmsTwin.Messages;

/// <summary>The body of <c>POST /v1/messages</c>.</summary>
internal sealed record SendMessageRequest(string? To, string? From, string? Body) : IJsonRequest
{
    public const string ToProblem = "'to' must be '+' followed by 8 to 15 digits";
    public const string FromProblem = "'from' must be a non-empty string";
    public const string BodyProblem = "'body' must be a non-empty string";

    public static string ProblemFor(string field) => field switch
    {
        "to" => ToProblem,
        "from" => FromProblem,
        "body" => BodyProblem,
        _ => JsonBody.NotJson,
    };
}

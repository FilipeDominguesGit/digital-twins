using SmsTwin.Http;

namespace SmsTwin.Inbound;

/// <summary>The body of <c>POST /twin/inbound</c>.</summary>
internal sealed record InboundMessageRequest(string? From, string? To, string? Body) : IJsonRequest
{
    public const string FromProblem = "'from' must be '+' followed by 8 to 15 digits";
    public const string ToProblem = "'to' must be a non-empty string";
    public const string BodyProblem = "'body' must be a string";

    public static string ProblemFor(string field) => field switch
    {
        "from" => FromProblem,
        "to" => ToProblem,
        "body" => BodyProblem,
        _ => JsonBody.NotJson,
    };
}

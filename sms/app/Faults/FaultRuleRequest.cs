using SmsTwin.Http;

namespace SmsTwin.Faults;

/// <summary>The body of <c>POST /twin/faults</c>.</summary>
internal sealed record FaultRuleRequest(
    FaultAction? Action,
    string? To,
    string? BodyContains,
    int? Status,
    int? DelayMs,
    int? Times) : IJsonRequest
{
    public const int MaxDelayMs = 120_000;
    public const string ActionProblem = "'action' must be 'error', 'delay' or 'accept-then-error'";
    public const string ToProblem = "'to' must be a string";
    public const string BodyContainsProblem = "'bodyContains' must be a non-empty string";
    public const string TimesProblem = "'times' must be a whole number of at least 1";
    public const string StatusProblem = "'status' must be a whole number from 100 to 599";
    public const string DelayMsProblem = "'delayMs' must be a whole number from 0 to 120000";
    public const string DelayNeedsDelayMs = "'delay' needs 'delayMs'";

    public static string ProblemFor(string field) => field switch
    {
        "action" => ActionProblem,
        "to" => ToProblem,
        "bodyContains" => BodyContainsProblem,
        "times" => TimesProblem,
        "status" => StatusProblem,
        "delayMs" => DelayMsProblem,
        _ => JsonBody.NotJson,
    };
}

using Microsoft.AspNetCore.Mvc;
using SmsTwin.Http;
using SmsTwin.Sms;

namespace SmsTwin.Faults;

/// <summary>The fault API: rules that make send requests fail on purpose.</summary>
internal static class FaultEndpoints
{
    public static void MapFaults(this IEndpointRouteBuilder app)
    {
        app.MapPost("/twin/faults", Add);
        app.MapGet("/twin/faults", ([FromServices] FaultRules faults) => Results.Json(faults.List()));
        app.MapDelete("/twin/faults", ([FromServices] FaultRules faults) =>
        {
            faults.Clear();
            return Results.NoContent();
        });
    }

    private static async Task<IResult> Add(HttpRequest request, [FromServices] FaultRules faults)
    {
        var (payload, error) = await JsonBody.Read<FaultRuleRequest>(request);
        if (payload is null)
        {
            return error!;
        }

        if (payload.Action is not { } action)
        {
            return ApiError.BadRequest(FaultRuleRequest.ActionProblem);
        }

        if (payload.To is { } to && !PhoneNumber.IsValid(to))
        {
            return ApiError.BadRequest(FaultRuleRequest.ToProblem);
        }

        if (payload.BodyContains is { Length: 0 })
        {
            return ApiError.BadRequest(FaultRuleRequest.BodyContainsProblem);
        }

        if (payload.Times is not { } times || times < 1)
        {
            return ApiError.BadRequest(FaultRuleRequest.TimesProblem);
        }

        if (payload.Status is < 100 or > 599)
        {
            return ApiError.BadRequest(FaultRuleRequest.StatusProblem);
        }

        if (payload.DelayMs is < 0 or > FaultRuleRequest.MaxDelayMs)
        {
            return ApiError.BadRequest(FaultRuleRequest.DelayMsProblem);
        }

        if (action == FaultAction.Delay && payload.DelayMs is null)
        {
            return ApiError.BadRequest(FaultRuleRequest.DelayNeedsDelayMs);
        }

        var rule = faults.Add(payload.To, payload.BodyContains, action, payload.Status, payload.DelayMs, times);
        return Results.Json(rule, statusCode: StatusCodes.Status201Created);
    }
}

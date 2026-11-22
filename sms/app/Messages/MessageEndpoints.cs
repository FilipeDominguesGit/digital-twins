using Microsoft.AspNetCore.Mvc;
using SmsTwin.Faults;
using SmsTwin.Http;
using SmsTwin.Sms;

namespace SmsTwin.Messages;

/// <summary>The send API (what a product calls) and the test API for the stored messages.</summary>
internal static class MessageEndpoints
{
    public static void MapMessages(this IEndpointRouteBuilder app, ApiKeyFilter apiKey)
    {
        app.MapPost("/v1/messages", Send).AddEndpointFilter(apiKey);
        app.MapGet("/twin/messages", ([FromQuery] string? to, [FromServices] MessageStore messages) => Results.Json(messages.List(to)));
        app.MapDelete("/twin/messages", ([FromServices] MessageStore messages) =>
        {
            messages.Clear();
            return Results.NoContent();
        });
    }

    // Authentication (the filter), then validation, then the fault rules, then idempotency.
    private static async Task<IResult> Send(
        HttpRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromServices] MessageStore messages,
        [FromServices] FaultRules faults)
    {
        var (payload, error) = await JsonBody.Read<SendMessageRequest>(request);
        if (payload is null)
        {
            return error!;
        }

        if (payload.To is not { } to || !PhoneNumber.IsValid(to))
        {
            return ApiError.BadRequest(SendMessageRequest.ToProblem);
        }

        if (string.IsNullOrEmpty(payload.From))
        {
            return ApiError.BadRequest(SendMessageRequest.FromProblem);
        }

        if (string.IsNullOrEmpty(payload.Body))
        {
            return ApiError.BadRequest(SendMessageRequest.BodyProblem);
        }

        if (payload.Body.Length > SmsLimits.MaxBodyLength)
        {
            return ApiError.BadRequest($"'body' is longer than {SmsLimits.MaxBodyLength} characters");
        }

        var send = new OutgoingMessage(to, payload.From, payload.Body, string.IsNullOrEmpty(idempotencyKey) ? null : idempotencyKey);
        var fault = faults.Take(to, payload.Body);
        if (fault is { Action: FaultAction.Delay })
        {
            await Task.Delay(fault.DelayMs ?? 0);
        }

        if (fault is { Action: FaultAction.Error })
        {
            return ApiError.Create(fault.Status ?? StatusCodes.Status503ServiceUnavailable, "fault injected: error");
        }

        var result = messages.Accept(send);
        if (result.IsConflict)
        {
            return ApiError.Create(StatusCodes.Status409Conflict, "Idempotency-Key reused with a different request");
        }

        if (fault is { Action: FaultAction.AcceptThenError })
        {
            return ApiError.Create(fault.Status ?? StatusCodes.Status500InternalServerError, "fault injected: accept-then-error");
        }

        return Results.Json(SendResponse.For(result.Message), statusCode: StatusCodes.Status202Accepted);
    }
}

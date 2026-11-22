using Microsoft.AspNetCore.Mvc;
using SmsTwin.Http;
using SmsTwin.Sms;

namespace SmsTwin.Inbound;

/// <summary>The test API for incoming messages: simulate one, deliver it again, list and clear them.</summary>
internal static class InboundEndpoints
{
    public static void MapInbound(this IEndpointRouteBuilder app)
    {
        app.MapPost("/twin/inbound", Add);
        app.MapPost("/twin/inbound/{id}/redeliver", Redeliver);
        app.MapGet("/twin/inbound", ([FromServices] InboundStore inbound) => Results.Json(inbound.List()));
        app.MapDelete("/twin/inbound", ([FromServices] InboundStore inbound) =>
        {
            inbound.Clear();
            return Results.NoContent();
        });
    }

    // Stores the message and delivers it, and only then answers 201.
    private static async Task<IResult> Add(HttpRequest request, [FromServices] InboundStore inbound, [FromServices] Webhook webhook)
    {
        var (payload, error) = await JsonBody.Read<InboundMessageRequest>(request);
        if (payload is null)
        {
            return error!;
        }

        if (payload.From is not { } from || !PhoneNumber.IsValid(from))
        {
            return ApiError.BadRequest(InboundMessageRequest.FromProblem);
        }

        if (string.IsNullOrEmpty(payload.To))
        {
            return ApiError.BadRequest(InboundMessageRequest.ToProblem);
        }

        if (payload.Body is null)
        {
            return ApiError.BadRequest(InboundMessageRequest.BodyProblem);
        }

        if (payload.Body.Length > SmsLimits.MaxBodyLength)
        {
            return ApiError.BadRequest($"'body' is longer than {SmsLimits.MaxBodyLength} characters");
        }

        var message = inbound.Add(from, payload.To, payload.Body);
        await webhook.Deliver(message);
        return Results.Json(message.View(), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> Redeliver([FromRoute] string id, [FromServices] InboundStore inbound, [FromServices] Webhook webhook)
    {
        if (inbound.Find(id) is not { } message)
        {
            return ApiError.Create(StatusCodes.Status404NotFound, "unknown incoming message");
        }

        await webhook.Deliver(message);
        return Results.Json(message.View());
    }
}

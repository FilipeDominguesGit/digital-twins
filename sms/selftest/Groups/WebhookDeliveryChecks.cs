using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using SmsTwin.SelfTest.Checking;
using SmsTwin.SelfTest.Client;
using static System.Net.HttpStatusCode;
using static SmsTwin.SelfTest.TestData;

namespace SmsTwin.SelfTest.Groups;

/// <summary>One delivery to the webhook: its timing, path, payload and signature.</summary>
internal sealed class WebhookDeliveryChecks : CheckGroup
{
    public override string Name => "Webhook delivery";

    public override GroupMode Mode => GroupMode.WithWebhook;

    public override async Task Run(CheckContext c)
    {
        var answer = await c.Twin.Inbound(Reply(" yes\n"));
        var message = answer.As<InboundView>();
        c.Check(
            "inbound: 201 with id and one attempt",
            answer.Status == Created && message.Id.StartsWith("in_", StringComparison.Ordinal)
            && message.Attempts is [{ Status: 204, Error: null } attempt] && attempt.At != default,
            answer.ToString());
        c.Check(
            "inbound: message fields",
            message.From == A && message.To == Sender && message.Body == " yes\n" && message.ReceivedAt != default,
            answer.ToString());
        c.Check("webhook: delivered once, before the 201", c.Webhook.Count == 1, $"{c.Webhook.Count} deliveries");

        c.Webhook.TryTake(out var delivery);
        var payload = JsonNode.Parse(delivery.Body);
        c.Check(
            "webhook: path and content type",
            delivery.Path == "/hook" && (delivery.ContentType ?? string.Empty).StartsWith("application/json", StringComparison.Ordinal),
            $"{delivery.Path} {delivery.ContentType}");
        c.Check(
            "webhook: payload fields",
            Text(payload, "id") == message.Id && Text(payload, "from") == A && Text(payload, "to") == Sender
            && Text(payload, "body") == " yes\n" && Text(payload, "receivedAt") == Text(answer.Json, "receivedAt"),
            payload?.ToJsonString() ?? string.Empty);
        c.Check("webhook: signature is HMAC-SHA256 of the raw body", delivery.Signature == c.Webhook.Sign(delivery.Body), $"{delivery.Signature}");
        c.Check(
            "webhook: signature is lowercase hex",
            delivery.Signature is { Length: 71 } signature && signature[7..].All(ch => char.IsAsciiDigit(ch) || ch is >= 'a' and <= 'f'),
            $"{delivery.Signature}");

        // Locks the exact bytes: web-default JSON (escaped non-ASCII and HTML characters), keys in this order.
        var special = await c.Twin.Inbound(Reply("Olá <b>&\"✓"));
        c.Webhook.TryTake(out var specialDelivery);
        var receivedAt = DateTimeOffset.Parse(Text(special.Json, "receivedAt"), CultureInfo.InvariantCulture);
        var expected = JsonSerializer.SerializeToUtf8Bytes(
            new { id = Text(special.Json, "id"), from = A, to = Sender, body = "Olá <b>&\"✓", receivedAt },
            JsonSerializerOptions.Web);
        c.Check(
            "webhook: body bytes are the compact web JSON of id, from, to, body, receivedAt",
            specialDelivery is not null && specialDelivery.Body.AsSpan().SequenceEqual(expected),
            specialDelivery is null ? "no delivery" : System.Text.Encoding.UTF8.GetString(specialDelivery.Body));
    }

    private static string Text(JsonNode? node, string key) => node?[key]?.ToString() ?? string.Empty;
}

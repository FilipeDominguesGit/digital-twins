using SmsTwin.SelfTest.Checking;
using SmsTwin.SelfTest.Client;
using static System.Net.HttpStatusCode;
using static SmsTwin.SelfTest.TestData;

namespace SmsTwin.SelfTest.Groups;

/// <summary>Locks the exact keys and value kinds of every answer, read as raw JSON.</summary>
internal sealed class ResponseShapeChecks : CheckGroup
{
    private static readonly string[] MessageKeys = ["id", "to", "from", "body", "encoding", "segments", "receivedAt", "idempotencyKey"];
    private static readonly string[] RuleKeys = ["id", "to", "bodyContains", "action", "status", "delayMs", "times"];
    private static readonly string[] InboundKeys = ["id", "from", "to", "body", "receivedAt", "attempts"];

    public override string Name => "Response shapes";

    public override async Task Run(CheckContext c)
    {
        var health = (await c.Twin.Health()).Json;
        c.Check("shape: health", JsonShape.HasExactly(health, "status", "version") && JsonShape.IsString(health!["version"]), $"{health}");

        await Error(c, "shape: 400 from the send API", c.Twin.Send(Message("hi", "bad")), BadRequest);
        await Error(c, "shape: 401", c.Twin.SendWithAuthorization(Message("hi"), null), Unauthorized);

        var sent = await c.Twin.Send(Message("hello"), idempotencyKey: "k-shape");
        c.Check(
            "shape: 202",
            JsonShape.HasExactly(sent.Json, "id", "status", "encoding", "segments") && JsonShape.IsNumber(sent.Json!["segments"]),
            sent.ToString());
        await Error(c, "shape: 409", c.Twin.Send(Message("other"), idempotencyKey: "k-shape"), Conflict);

        var listed = await c.Twin.MessagesWithQuery(string.Empty);
        var stored = listed.Json?[0];
        c.Check(
            "shape: a stored message",
            JsonShape.HasExactly(stored, MessageKeys) && JsonShape.IsNumber(stored!["segments"]) && JsonShape.IsString(stored["idempotencyKey"]),
            listed.ToString());
        var emptyTo = await c.Twin.MessagesWithQuery("?to=");
        c.Check("shape: an empty 'to' filter matches no message", emptyTo.Status == OK && emptyTo.Text == "[]", emptyTo.ToString());

        var rule = await c.Twin.AddFault(new FaultSpec("accept-then-error", Times: 1, To: B, Status: 502));
        c.Check(
            "shape: a fault rule",
            rule.Status == Created && JsonShape.HasExactly(rule.Json, RuleKeys) && rule.Json!["action"]!.ToString() == "accept-then-error"
            && JsonShape.IsString(rule.Json["action"]) && JsonShape.IsNumber(rule.Json["times"]) && JsonShape.IsNumber(rule.Json["status"])
            && rule.Json["delayMs"] is null && rule.Json["bodyContains"] is null,
            rule.ToString());
        var rules = await c.Twin.FaultsResponse();
        c.Check("shape: GET faults", JsonShape.HasExactly(rules.Json?[0], RuleKeys), rules.ToString());
        await Error(c, "shape: 400 from the fault API", c.Twin.AddFaultJson("""{"action":"explode","times":1}"""), BadRequest);
        await c.Twin.ClearFaults();
        await c.Twin.AddFault(new FaultSpec("error", Times: 1));
        await Error(c, "shape: an injected error", c.Twin.Send(Message("fail")), ServiceUnavailable);

        var inbound = await c.Twin.Inbound(Reply("YES"));
        var attempt = inbound.Json?["attempts"]?[0];
        c.Check(
            "shape: an incoming message",
            inbound.Status == Created && JsonShape.HasExactly(inbound.Json, InboundKeys) && JsonShape.HasExactly(attempt, "at", "status", "error"),
            inbound.ToString());
        var inboundList = await c.Twin.InboundResponse();
        c.Check("shape: GET inbound", JsonShape.HasExactly(inboundList.Json?[0], InboundKeys), inboundList.ToString());
        await Error(c, "shape: 400 from the inbound API", c.Twin.InboundJson("[]"), BadRequest);
        await Error(c, "shape: 404 for redeliver", c.Twin.Redeliver("in_unknown"), NotFound);

        var deletes = new[] { await c.Twin.ClearMessages(), await c.Twin.ClearFaults(), await c.Twin.ClearInbound() };
        c.Check("shape: DELETEs answer 204 with no body", deletes.All(d => d.Status == NoContent && d.Text.Length == 0), string.Join(", ", deletes));
    }

    private static async Task Error(CheckContext c, string name, Task<TwinResponse> request, System.Net.HttpStatusCode status)
    {
        var answer = await request;
        c.Check(name, answer.Status == status && JsonShape.IsError(answer.Json), answer.ToString());
    }
}

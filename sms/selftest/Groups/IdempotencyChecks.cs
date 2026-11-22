using SmsTwin.SelfTest.Checking;
using SmsTwin.SelfTest.Client;
using static System.Net.HttpStatusCode;
using static SmsTwin.SelfTest.TestData;

namespace SmsTwin.SelfTest.Groups;

internal sealed class IdempotencyChecks : CheckGroup
{
    public override string Name => "Idempotency";

    public override async Task Run(CheckContext c)
    {
        var first = await c.Twin.Send(Message("same"), idempotencyKey: "k1");
        var second = await c.Twin.Send(Message("same"), idempotencyKey: "k1");
        c.Check(
            "repeated key replays the first response",
            first.Status == Accepted && second.Status == Accepted && first.As<SendResult>().Id == second.As<SendResult>().Id);
        c.Check("repeated key stores nothing new", (await c.Twin.Messages()).Count == 1);
        c.Check("same key, other body: 409", (await c.Twin.Send(Message("other"), idempotencyKey: "k1")).Status == Conflict);
        c.Check("same key, other number: 409", (await c.Twin.Send(Message("same", B), idempotencyKey: "k1")).Status == Conflict);
        c.Check("still one message after 409s", (await c.Twin.Messages()).Count == 1);
    }
}

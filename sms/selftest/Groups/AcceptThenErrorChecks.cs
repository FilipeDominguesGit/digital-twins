using SmsTwin.SelfTest.Checking;
using SmsTwin.SelfTest.Client;
using static System.Net.HttpStatusCode;
using static SmsTwin.SelfTest.TestData;

namespace SmsTwin.SelfTest.Groups;

/// <summary>An <c>accept-then-error</c> rule, and a retry with the same idempotency key.</summary>
internal sealed class AcceptThenErrorChecks : CheckGroup
{
    public override string Name => "Faults: accept-then-error";

    public override async Task Run(CheckContext c)
    {
        await c.Twin.AddFault(new FaultSpec("accept-then-error", Times: 1));
        c.Check("accept-then-error answers 500", (await c.Twin.Send(Message("once"), idempotencyKey: "k-ate")).Status == InternalServerError);
        c.Check("accept-then-error stores the message", (await c.Twin.Messages()).Count == 1);
        var retry = await c.Twin.Send(Message("once"), idempotencyKey: "k-ate");
        c.Check(
            "retry with the same key: 202, same id",
            retry.Status == Accepted && retry.As<SendResult>().Id == (await c.Twin.Messages())[0].Id,
            retry.ToString());
        c.Check("retry stores nothing new", (await c.Twin.Messages()).Count == 1);
        await c.Twin.AddFault(new FaultSpec("accept-then-error", Times: 1, Status: 502));
        c.Check("accept-then-error custom status", (await c.Twin.Send(Message("twice"))).Status == BadGateway);
        await c.Twin.ClearFaults();
        c.Check("DELETE clears faults", (await c.Twin.Faults()).Count == 0);
    }
}

using SmsTwin.SelfTest.Checking;
using SmsTwin.SelfTest.Client;
using static System.Net.HttpStatusCode;
using static SmsTwin.SelfTest.TestData;

namespace SmsTwin.SelfTest.Groups;

/// <summary>Two messages to one number at the same time: <c>bodyContains</c> picks the right send.</summary>
internal sealed class SameNumberFaultChecks : CheckGroup
{
    public override string Name => "Faults: two messages to one number";

    public override async Task Run(CheckContext c)
    {
        await c.Twin.AddFault(new FaultSpec("accept-then-error", Times: 1, To: A, BodyContains: "Beta"));
        c.Check("same number: the other text is not hit", (await c.Twin.Send(Message("Alpha reminder"), idempotencyKey: "k-alpha")).Status == Accepted);
        c.Check("same number: the matching text is hit", (await c.Twin.Send(Message("Beta request"), idempotencyKey: "k-beta")).Status == InternalServerError);
        c.Check("same number: both stored once", (await c.Twin.Messages(A)).Count == 2);
    }
}

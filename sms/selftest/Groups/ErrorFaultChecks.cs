using SmsTwin.SelfTest.Checking;
using SmsTwin.SelfTest.Client;
using static System.Net.HttpStatusCode;
using static SmsTwin.SelfTest.TestData;

namespace SmsTwin.SelfTest.Groups;

/// <summary>An <c>error</c> rule matched by number, used up after its <c>times</c>.</summary>
internal sealed class ErrorFaultChecks : CheckGroup
{
    public override string Name => "Error faults";

    public override async Task Run(CheckContext c)
    {
        c.Check("fault added: 201", (await c.Twin.AddFault(new FaultSpec("error", Times: 2, To: B, Status: 503))).Status == Created);
        c.Check("fault does not match another number", (await c.Twin.Send(Message("to A"))).Status == Accepted);
        c.Check("error fault 1 of 2", (await c.Twin.Send(Message("to B", B))).Status == ServiceUnavailable);
        var left = await c.Twin.Faults();
        c.Check("GET faults shows times left", left is [{ Times: 1 }], string.Join(", ", left));
        c.Check("error fault 2 of 2", (await c.Twin.Send(Message("to B", B))).Status == ServiceUnavailable);
        c.Check("fault used up: 202", (await c.Twin.Send(Message("to B", B))).Status == Accepted);
        c.Check("error stores nothing", (await c.Twin.Messages(B)).Count == 1);
        c.Check("used-up rule is gone", (await c.Twin.Faults()).Count == 0);
    }
}

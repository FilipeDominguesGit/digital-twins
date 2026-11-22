using SmsTwin.SelfTest.Checking;
using SmsTwin.SelfTest.Client;
using static System.Net.HttpStatusCode;
using static SmsTwin.SelfTest.TestData;

namespace SmsTwin.SelfTest.Groups;

internal sealed class FaultOrderChecks : CheckGroup
{
    public override string Name => "Fault order and default status";

    public override async Task Run(CheckContext c)
    {
        await c.Twin.AddFault(new FaultSpec("error", Times: 1));
        await c.Twin.AddFault(new FaultSpec("error", Times: 1, Status: 429));
        c.Check("error default status 503", (await c.Twin.Send(Message("x"))).Status == ServiceUnavailable);
        c.Check("rules apply in creation order", (await c.Twin.Send(Message("x"))).Status == TooManyRequests);
    }
}

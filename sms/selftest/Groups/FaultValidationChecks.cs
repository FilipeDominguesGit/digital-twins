using SmsTwin.SelfTest.Checking;
using SmsTwin.SelfTest.Client;
using static System.Net.HttpStatusCode;

namespace SmsTwin.SelfTest.Groups;

internal sealed class FaultValidationChecks : CheckGroup
{
    public override string Name => "Fault validation";

    public override async Task Run(CheckContext c)
    {
        c.Check("fault: unknown action 400", (await c.Twin.AddFault(new FaultSpec("explode", Times: 1))).Status == BadRequest);
        c.Check("fault: times 0 is 400", (await c.Twin.AddFault(new FaultSpec("error", Times: 0))).Status == BadRequest);
        c.Check("fault: missing times is 400", (await c.Twin.AddFault(new FaultSpec("error"))).Status == BadRequest);
        c.Check("fault: status 600 is 400", (await c.Twin.AddFault(new FaultSpec("error", Times: 1, Status: 600))).Status == BadRequest);
        c.Check("fault: delay without delayMs is 400", (await c.Twin.AddFault(new FaultSpec("delay", Times: 1))).Status == BadRequest);
        c.Check("fault: no rules after rejected ones", (await c.Twin.Faults()).Count == 0);
    }
}

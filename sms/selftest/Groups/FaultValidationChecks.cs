using SmsTwin.SelfTest.Checking;
using SmsTwin.SelfTest.Client;
using static System.Net.HttpStatusCode;
using static SmsTwin.SelfTest.TestData;

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
        var badFault = await c.Twin.AddFault(new FaultSpec("error", Times: 1, To: "abc"));
        var badSend = await c.Twin.Send(Message("hi", "abc"));
        c.Check(
            "fault: a bad to gets the send API's 400 (0.4.0)",
            badFault.Status == BadRequest && badFault.Json?["error"]?.ToString() == badSend.Json?["error"]?.ToString(),
            $"{badFault} / {badSend}");
        c.Check("fault: no rules after rejected ones", (await c.Twin.Faults()).Count == 0);
    }
}

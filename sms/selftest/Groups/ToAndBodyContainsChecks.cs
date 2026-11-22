using SmsTwin.SelfTest.Checking;
using SmsTwin.SelfTest.Client;
using static System.Net.HttpStatusCode;
using static SmsTwin.SelfTest.TestData;

namespace SmsTwin.SelfTest.Groups;

internal sealed class ToAndBodyContainsChecks : CheckGroup
{
    public override string Name => "Faults: to and bodyContains together";

    public override async Task Run(CheckContext c)
    {
        await c.Twin.AddFault(new FaultSpec("error", Times: 2, To: B, BodyContains: "Alpha"));
        c.Check("to and bodyContains: right text, other number: 202", (await c.Twin.Send(Message("Alpha", A))).Status == Accepted);
        c.Check("to and bodyContains: right number, other text: 202", (await c.Twin.Send(Message("Beta", B))).Status == Accepted);
        c.Check("to and bodyContains: both match: 503", (await c.Twin.Send(Message("Alpha", B))).Status == ServiceUnavailable);
        c.Check("to and bodyContains: one use left", await c.Twin.Faults() is [{ Times: 1 }]);
    }
}

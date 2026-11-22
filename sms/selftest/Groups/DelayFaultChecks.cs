using System.Diagnostics;
using SmsTwin.SelfTest.Checking;
using SmsTwin.SelfTest.Client;
using static System.Net.HttpStatusCode;
using static SmsTwin.SelfTest.TestData;

namespace SmsTwin.SelfTest.Groups;

internal sealed class DelayFaultChecks : CheckGroup
{
    public override string Name => "Faults: delay";

    public override async Task Run(CheckContext c)
    {
        await c.Twin.AddFault(new FaultSpec("delay", Times: 1, DelayMs: 1500));
        var clock = Stopwatch.StartNew();
        var answer = await c.Twin.Send(Message("slow"));
        c.Check(
            "delay then handled normally",
            answer.Status == Accepted && clock.ElapsedMilliseconds >= 1400,
            $"{answer.Status} after {clock.ElapsedMilliseconds} ms");
    }
}

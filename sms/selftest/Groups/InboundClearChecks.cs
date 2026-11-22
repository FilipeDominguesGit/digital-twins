using SmsTwin.SelfTest.Checking;
using static System.Net.HttpStatusCode;
using static SmsTwin.SelfTest.TestData;

namespace SmsTwin.SelfTest.Groups;

internal sealed class InboundClearChecks : CheckGroup
{
    public override string Name => "Incoming messages: clearing";

    public override async Task Run(CheckContext c)
    {
        await c.Twin.Inbound(Reply("YES"));
        c.Check("DELETE inbound: 204", (await c.Twin.ClearInbound()).Status == NoContent);
        c.Check("DELETE inbound clears them", (await c.Twin.InboundList()).Count == 0);
        c.Check("inbound does not touch sent messages", (await c.Twin.Messages()).Count == 0);
    }
}

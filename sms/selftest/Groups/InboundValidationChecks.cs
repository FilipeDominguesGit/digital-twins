using SmsTwin.SelfTest.Checking;
using static System.Net.HttpStatusCode;
using static SmsTwin.SelfTest.TestData;

namespace SmsTwin.SelfTest.Groups;

internal sealed class InboundValidationChecks : CheckGroup
{
    public override string Name => "Incoming messages: validation";

    public override async Task Run(CheckContext c)
    {
        c.Check("inbound: 400 for bad from", (await c.Twin.Inbound(Reply("YES", "912345678"))).Status == BadRequest);
        c.Check("inbound: 400 for from with a trailing newline (0.4.0)", (await c.Twin.Inbound(Reply("YES", A + "\n"))).Status == BadRequest);
        c.Check("inbound: 400 for empty to", (await c.Twin.Inbound(new(A, string.Empty, "YES"))).Status == BadRequest);
        c.Check("inbound: 400 for missing body", (await c.Twin.InboundJson($$"""{"from":"{{A}}","to":"{{Sender}}"}""")).Status == BadRequest);
        c.Check("inbound: 400 for body over 1600", (await c.Twin.Inbound(Reply(new string('a', 1601)))).Status == BadRequest);
        c.Check("inbound: nothing stored by rejected requests", (await c.Twin.InboundList()).Count == 0);
    }
}

using SmsTwin.SelfTest.Checking;
using SmsTwin.SelfTest.Client;
using static System.Net.HttpStatusCode;
using static SmsTwin.SelfTest.TestData;

namespace SmsTwin.SelfTest.Groups;

internal sealed class InboundWithoutWebhookChecks : CheckGroup
{
    public override string Name => "Incoming messages: no webhook";

    public override GroupMode Mode => GroupMode.WithoutWebhook;

    public override async Task Run(CheckContext c)
    {
        var answer = await c.Twin.Inbound(Reply("YES"));
        var message = answer.As<InboundView>();
        c.Check(
            "inbound without webhook: 201, not-configured",
            answer.Status == Created && message.Attempts is [{ Status: null, Error: "not-configured" }],
            answer.ToString());
    }
}

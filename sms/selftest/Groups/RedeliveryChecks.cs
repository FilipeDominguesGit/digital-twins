using SmsTwin.SelfTest.Checking;
using SmsTwin.SelfTest.Client;
using static System.Net.HttpStatusCode;
using static SmsTwin.SelfTest.TestData;

namespace SmsTwin.SelfTest.Groups;

internal sealed class RedeliveryChecks : CheckGroup
{
    public override string Name => "Webhook redelivery";

    public override GroupMode Mode => GroupMode.WithWebhook;

    public override async Task Run(CheckContext c)
    {
        var message = (await c.Twin.Inbound(Reply(" yes\n"))).As<InboundView>();
        c.Webhook.TryTake(out var first);

        var answer = await c.Twin.Redeliver(message.Id);
        c.Webhook.TryTake(out var second);
        c.Check("redeliver: 200, two attempts", answer.Status == OK && answer.As<InboundView>().Attempts.Count == 2, answer.ToString());
        c.Check(
            "redeliver: same bytes and signature",
            first is not null && second is not null && second.Body.AsSpan().SequenceEqual(first.Body) && second.Signature == first.Signature);
        c.Check("redeliver: 404 for an unknown id", (await c.Twin.Redeliver("in_unknown")).Status == NotFound);
    }
}

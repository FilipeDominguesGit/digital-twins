using SmsTwin.SelfTest.Checking;
using SmsTwin.SelfTest.Client;
using static System.Net.HttpStatusCode;
using static SmsTwin.SelfTest.TestData;

namespace SmsTwin.SelfTest.Groups;

/// <summary>A webhook that answers 500: recorded, never retried; the list keeps every attempt.</summary>
internal sealed class WebhookFailureChecks : CheckGroup
{
    public override string Name => "Webhook failure";

    public override GroupMode Mode => GroupMode.WithWebhook;

    public override async Task Run(CheckContext c)
    {
        var first = (await c.Twin.Inbound(Reply("YES"))).As<InboundView>();
        await c.Twin.Redeliver(first.Id);
        c.Webhook.Clear();

        c.Webhook.Reply = 500;
        var answer = await c.Twin.Inbound(Reply("NO", B));
        await Task.Delay(300);
        c.Check(
            "webhook 500: recorded, no retry",
            answer.Status == Created && answer.As<InboundView>().Attempts is [{ Status: 500 }] && c.Webhook.Count == 1,
            $"{answer} deliveries {c.Webhook.Count}");
        c.Webhook.Reply = 204;

        var list = await c.Twin.InboundList();
        c.Check(
            "GET inbound: in order, with attempts",
            list.Count == 2 && list[0].Id == first.Id && list[0].Attempts.Count == 2 && list[1].From == B,
            string.Join(", ", list));
    }
}

using System.Diagnostics;
using SmsTwin.SelfTest.Checking;
using SmsTwin.SelfTest.Client;
using static System.Net.HttpStatusCode;
using static SmsTwin.SelfTest.TestData;

namespace SmsTwin.SelfTest.Groups;

internal sealed class WebhookTimeoutChecks : CheckGroup
{
    public override string Name => "Webhook timeout";

    public override GroupMode Mode => GroupMode.WithWebhook;

    public override async Task Run(CheckContext c)
    {
        c.Webhook.ReplyDelayMs = 11_000;
        var clock = Stopwatch.StartNew();
        var answer = await c.Twin.Inbound(Reply("late"));
        c.Check(
            "webhook timeout after 10 s",
            answer.Status == Created && answer.As<InboundView>().Attempts is [{ Status: null, Error: "timeout" }]
            && clock.ElapsedMilliseconds is >= 9_500 and < 11_000,
            $"{answer} after {clock.ElapsedMilliseconds} ms");
        c.Webhook.ReplyDelayMs = 0;

        // Let the listener finish the late answer before the next group starts.
        await Task.Delay(1_500);
    }
}

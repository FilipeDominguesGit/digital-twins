using SmsTwin.SelfTest.Client;
using SmsTwin.SelfTest.Webhook;

namespace SmsTwin.SelfTest.Checking;

/// <summary>What a group works with: the twin, the webhook listener when there is one, and the checker.</summary>
internal sealed class CheckContext(TwinClient twin, WebhookListener? webhook, Checker checker)
{
    public TwinClient Twin { get; } = twin;

    public WebhookListener Webhook => webhook ?? throw new InvalidOperationException("this group needs the webhook arguments");

    public void Check(string name, bool ok, string detail = "") => checker.Check(name, ok, detail);
}

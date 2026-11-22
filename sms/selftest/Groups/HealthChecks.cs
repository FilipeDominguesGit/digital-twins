using System.Net;
using SmsTwin.SelfTest.Checking;
using SmsTwin.SelfTest.Client;

namespace SmsTwin.SelfTest.Groups;

internal sealed class HealthChecks : CheckGroup
{
    public override string Name => "Health";

    public override async Task Run(CheckContext c)
    {
        var answer = await c.Twin.Health();
        var health = answer.As<HealthView>();
        c.Check("health", answer.Status == HttpStatusCode.OK && health.Status == "ok" && health.Version.Length > 0, answer.ToString());
    }
}

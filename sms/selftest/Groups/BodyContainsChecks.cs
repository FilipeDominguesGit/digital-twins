using SmsTwin.SelfTest.Checking;
using SmsTwin.SelfTest.Client;
using static System.Net.HttpStatusCode;
using static SmsTwin.SelfTest.TestData;

namespace SmsTwin.SelfTest.Groups;

/// <summary>The <c>bodyContains</c> filter of fault rules (0.3.0).</summary>
internal sealed class BodyContainsChecks : CheckGroup
{
    public override string Name => "Faults: bodyContains";

    public override async Task Run(CheckContext c)
    {
        c.Check("fault: empty bodyContains is 400", (await c.Twin.AddFault(new FaultSpec("error", Times: 1, BodyContains: string.Empty))).Status == BadRequest);
        c.Check("fault: number bodyContains is 400", (await c.Twin.AddFaultJson("""{"action":"error","bodyContains":5,"times":1}""")).Status == BadRequest);
        c.Check("fault: boolean bodyContains is 400", (await c.Twin.AddFaultJson("""{"action":"error","bodyContains":true,"times":1}""")).Status == BadRequest);
        c.Check("fault: array bodyContains is 400", (await c.Twin.AddFaultJson("""{"action":"error","bodyContains":["Alpha"],"times":1}""")).Status == BadRequest);
        c.Check("fault: no rules after rejected bodyContains", (await c.Twin.Faults()).Count == 0);

        var created = await c.Twin.AddFault(new FaultSpec("error", Times: 1, BodyContains: "Alpha notice", Status: 429));
        var rule = created.As<FaultRuleView>();
        c.Check(
            "fault with bodyContains: 201 shows it",
            created.Status == Created && rule.BodyContains == "Alpha notice" && rule.Id.StartsWith("fault_", StringComparison.Ordinal),
            created.ToString());
        await c.Twin.AddFault(new FaultSpec("error", Times: 1, Status: 503));
        var listed = await c.Twin.FaultsResponse();
        var rules = listed.As<List<FaultRuleView>>();
        var second = listed.Json?[1]?.AsObject();
        c.Check(
            "GET faults: bodyContains, and null when not given",
            rules.Count == 2 && rules[0].BodyContains == "Alpha notice" && second is not null
            && second.ContainsKey("bodyContains") && second["bodyContains"] is null,
            listed.ToString());
        c.Check("bodyContains: send without the text takes the next rule", (await c.Twin.Send(Message("Beta notice: hello"))).Status == ServiceUnavailable);
        rules = [.. await c.Twin.Faults()];
        c.Check("bodyContains: skipped rule keeps its times", rules is [{ BodyContains: "Alpha notice", Times: 1 }], string.Join(", ", rules));
        c.Check("bodyContains: send without the text, no other rule: 202", (await c.Twin.Send(Message("Beta notice: hello"))).Status == Accepted);
        c.Check("bodyContains is case-sensitive", (await c.Twin.Send(Message("alpha notice: hello"))).Status == Accepted);
        c.Check("bodyContains: send with the text uses the rule", (await c.Twin.Send(Message("Hi. Alpha notice: hello"))).Status == TooManyRequests);
        c.Check("bodyContains: rule used up", (await c.Twin.Faults()).Count == 0);
        c.Check("bodyContains: only the sends without a fault were stored", (await c.Twin.Messages()).Count == 2);
    }
}

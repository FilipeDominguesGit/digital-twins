using SmsTwin.SelfTest.Checking;
using SmsTwin.SelfTest.Client;
using static System.Net.HttpStatusCode;
using static SmsTwin.SelfTest.TestData;

namespace SmsTwin.SelfTest.Groups;

/// <summary>Locks the order of the send API: authentication, validation, fault rules, then idempotency.</summary>
internal sealed class SendOrderChecks : CheckGroup
{
    public override string Name => "Send API: order of checks";

    public override async Task Run(CheckContext c)
    {
        c.Check("order: 401 comes before validation", (await c.Twin.SendJsonWithoutKey("{not json")).Status == Unauthorized);

        await c.Twin.AddFault(new FaultSpec("error", Times: 1));
        var refused = await c.Twin.Send(Message("hi", "912345678"));
        c.Check("order: a refused send does not use a fault rule", refused.Status == BadRequest && await c.Twin.Faults() is [{ Times: 1 }], refused.ToString());
        var unauthorized = await c.Twin.SendWithAuthorization(Message("hi"), null);
        c.Check("order: an unauthorized send does not use a fault rule", unauthorized.Status == Unauthorized && await c.Twin.Faults() is [{ Times: 1 }]);
        c.Check("order: the rule then applies to the next valid send", (await c.Twin.Send(Message("hi"))).Status == ServiceUnavailable && (await c.Twin.Faults()).Count == 0);

        await c.Twin.Send(Message("first"), idempotencyKey: "k-order");
        await c.Twin.AddFault(new FaultSpec("error", Times: 1));
        var conflictWithError = await c.Twin.Send(Message("other"), idempotencyKey: "k-order");
        c.Check(
            "order: an error rule applies before the idempotency check",
            conflictWithError.Status == ServiceUnavailable && (await c.Twin.Faults()).Count == 0,
            conflictWithError.ToString());

        await c.Twin.AddFault(new FaultSpec("accept-then-error", Times: 1));
        var conflictWithAccept = await c.Twin.Send(Message("third"), idempotencyKey: "k-order");
        c.Check(
            "order: accept-then-error with a conflicting key: 409, rule used",
            conflictWithAccept.Status == Conflict && (await c.Twin.Faults()).Count == 0 && (await c.Twin.Messages()).Count == 1,
            conflictWithAccept.ToString());
    }
}

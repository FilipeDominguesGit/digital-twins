using SmsTwin.SelfTest.Checking;
using SmsTwin.SelfTest.Client;
using static System.Net.HttpStatusCode;
using static SmsTwin.SelfTest.TestData;

namespace SmsTwin.SelfTest.Groups;

internal sealed class MessageInspectionChecks : CheckGroup
{
    public override string Name => "Messages";

    public override async Task Run(CheckContext c)
    {
        var firstAnswer = await c.Twin.Send(Message("hello A"), idempotencyKey: "k-shape");
        await c.Twin.Send(Message("hello B", B));
        var first = firstAnswer.As<SendResult>();
        var all = await c.Twin.Messages();
        var m0 = all[0];
        c.Check(
            "202 response shape",
            firstAnswer.Status == Accepted && first.Id.StartsWith("sms_", StringComparison.Ordinal) && first.Status == "accepted",
            firstAnswer.ToString());
        c.Check(
            "message fields",
            m0.Id == first.Id && m0.To == A && m0.From == Sender && m0.Body == "hello A"
            && m0.Encoding == "GSM-7" && m0.Segments == 1 && m0.IdempotencyKey == "k-shape" && m0.ReceivedAt != default,
            m0.ToString());
        c.Check("messages in order received", all.Count == 2 && all[1].To == B);
        var toB = await c.Twin.Messages(B);
        c.Check("filter by to", toB.Count == 1 && toB[0].Body == "hello B");
        c.Check("no idempotency key is null", all[1].IdempotencyKey is null);
        await c.Twin.ClearMessages();
        c.Check("DELETE clears messages", (await c.Twin.Messages()).Count == 0);
        var afterClear = await c.Twin.Send(Message("hello A"), idempotencyKey: "k-shape");
        c.Check("DELETE clears idempotency keys", afterClear.Status == Accepted && (await c.Twin.Messages()).Count == 1);
    }
}

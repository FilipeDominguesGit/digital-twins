using SmsTwin.SelfTest.Checking;
using static System.Net.HttpStatusCode;
using static SmsTwin.SelfTest.TestData;

namespace SmsTwin.SelfTest.Groups;

internal sealed class SendValidationChecks : CheckGroup
{
    public override string Name => "Send validation";

    public override async Task Run(CheckContext c)
    {
        foreach (var to in new[] { "912345678", "+1234567", "+1234567890123456", "+35191 000001", string.Empty })
        {
            c.Check($"400 for to '{to}'", (await c.Twin.Send(Message("hi", to))).Status == BadRequest);
        }

        c.Check("400 for to with a trailing newline (0.4.0)", (await c.Twin.Send(Message("hi", A + "\n"))).Status == BadRequest);
        c.Check("400 for to with a trailing CR LF (0.4.0)", (await c.Twin.Send(Message("hi", A + "\r\n"))).Status == BadRequest);
        c.Check("400 for empty body", (await c.Twin.Send(Message(string.Empty))).Status == BadRequest);
        c.Check("400 for body over 1600", (await c.Twin.Send(Message(new string('a', 1601)))).Status == BadRequest);
        c.Check("400 for missing from", (await c.Twin.SendJson($$"""{"to":"{{A}}","body":"hi"}""")).Status == BadRequest);
        c.Check("400 for invalid JSON", (await c.Twin.SendJson("{not json")).Status == BadRequest);
        c.Check("400 for a JSON array", (await c.Twin.SendJson("[]")).Status == BadRequest);
        c.Check(
            "8 and 15 digits accepted",
            (await c.Twin.Send(Message("hi", "+12345678"))).Status == Accepted
            && (await c.Twin.Send(Message("hi", "+123456789012345"))).Status == Accepted);
        c.Check("1600 characters accepted", (await c.Twin.Send(Message(new string('a', 1600)))).Status == Accepted);
        var stored = await c.Twin.Messages();
        c.Check("nothing stored by rejected requests", stored.Count == 3, $"{stored.Count} messages");
    }
}

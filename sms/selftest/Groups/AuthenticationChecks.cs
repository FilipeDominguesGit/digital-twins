using SmsTwin.SelfTest.Checking;
using static System.Net.HttpStatusCode;
using static SmsTwin.SelfTest.TestData;

namespace SmsTwin.SelfTest.Groups;

internal sealed class AuthenticationChecks : CheckGroup
{
    public override string Name => "Authentication";

    public override async Task Run(CheckContext c)
    {
        c.Check("401 without key", (await c.Twin.SendWithAuthorization(Message("hi"), null)).Status == Unauthorized);
        c.Check("401 with a wrong key", (await c.Twin.SendWithAuthorization(Message("hi"), "Bearer wrong")).Status == Unauthorized);
    }
}

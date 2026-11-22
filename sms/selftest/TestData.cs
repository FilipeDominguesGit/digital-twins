using SmsTwin.SelfTest.Client;

namespace SmsTwin.SelfTest;

/// <summary>Synthetic numbers and the requests built from them.</summary>
internal static class TestData
{
    public const string A = "+351910000001";
    public const string B = "+351910000002";
    public const string Sender = "Twin";

    public static SmsRequest Message(string body, string to = A) => new(to, Sender, body);

    public static InboundRequest Reply(string body, string from = A) => new(from, Sender, body);
}

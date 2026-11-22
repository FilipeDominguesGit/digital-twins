using SmsTwin.SelfTest.Checking;
using SmsTwin.SelfTest.Client;
using static System.Net.HttpStatusCode;
using static SmsTwin.SelfTest.TestData;

namespace SmsTwin.SelfTest.Groups;

internal sealed class EncodingChecks : CheckGroup
{
    private static readonly (string Name, string Body, string Encoding, int Segments)[] Cases =
    [
        ("GSM-7 160 chars, 1 segment", new string('a', 160), "GSM-7", 1),
        ("GSM-7 161 chars, 2 segments", new string('a', 161), "GSM-7", 2),
        ("GSM-7 306 chars, 2 segments", new string('a', 306), "GSM-7", 2),
        ("GSM-7 307 chars, 3 segments", new string('a', 307), "GSM-7", 3),
        ("extension char counts 2 (160 septets)", new string('a', 158) + "€", "GSM-7", 1),
        ("extension char counts 2 (161 septets)", new string('a', 159) + "€", "GSM-7", 2),
        ("GSM accented letters stay GSM-7", "Olé, Müller, Ñaño, où, à Ça", "GSM-7", 1),
        ("accented letter outside GSM is UCS-2", "Ñandú", "UCS-2", 1),
        ("UCS-2 70 units, 1 segment", new string('a', 69) + "ã", "UCS-2", 1),
        ("UCS-2 71 units, 2 segments", new string('a', 70) + "ã", "UCS-2", 2),
        ("UCS-2 134 units, 2 segments", new string('a', 133) + "ã", "UCS-2", 2),
        ("UCS-2 135 units, 3 segments", new string('a', 134) + "ã", "UCS-2", 3),
        ("emoji is two UTF-16 units", new string('a', 68) + "😀", "UCS-2", 1),
        ("backtick is not GSM", "a`b", "UCS-2", 1),
    ];

    public override string Name => "Encoding and segments";

    public override async Task Run(CheckContext c)
    {
        foreach (var (name, body, encoding, segments) in Cases)
        {
            var answer = await c.Twin.Send(Message(body));
            var ok = answer.Status == Accepted && answer.As<SendResult>() is var sent && sent.Encoding == encoding && sent.Segments == segments;
            c.Check(name, ok, answer.ToString());
        }
    }
}

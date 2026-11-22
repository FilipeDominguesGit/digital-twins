using SmsTwin.SelfTest.Checking;
using static System.Net.HttpStatusCode;
using static SmsTwin.SelfTest.TestData;

namespace SmsTwin.SelfTest.Groups;

/// <summary>Locks what <c>POST /twin/inbound</c> refuses and accepts beyond the README's list (D-028).</summary>
internal sealed class InboundStrictInputChecks : CheckGroup
{
    private static readonly (string Name, string Json)[] Refused =
    [
        ("'body' as a number", $$"""{"from":"{{A}}","to":"{{Sender}}","body":5}"""),
        ("'body' as null", $$"""{"from":"{{A}}","to":"{{Sender}}","body":null}"""),
        ("'body' as an array", $$"""{"from":"{{A}}","to":"{{Sender}}","body":["YES"]}"""),
        ("'body' as an object", $$$"""{"from":"{{{A}}}","to":"{{{Sender}}}","body":{}}"""),
        ("'from' as a number", $$"""{"from":351910000001,"to":"{{Sender}}","body":"YES"}"""),
        ("'to' as a number", $$"""{"from":"{{A}}","to":5,"body":"YES"}"""),
        ("missing 'to'", $$"""{"from":"{{A}}","body":"YES"}"""),
        ("key 'From' with a capital", $$"""{"From":"{{A}}","to":"{{Sender}}","body":"YES"}"""),
        ("key 'To' with a capital", $$"""{"from":"{{A}}","To":"{{Sender}}","body":"YES"}"""),
        ("key 'Body' with a capital", $$"""{"from":"{{A}}","to":"{{Sender}}","Body":"YES"}"""),
        ("a duplicate key", $$"""{"from":"{{A}}","to":"{{Sender}}","body":"YES","body":"NO"}"""),
        ("invalid JSON", "{not json"),
        ("a JSON array", "[]"),
        ("JSON null", "null"),
        ("a trailing comma", $$"""{"from":"{{A}}","to":"{{Sender}}","body":"YES",}"""),
        ("an empty body", string.Empty),
    ];

    public override string Name => "Incoming messages: strict input";

    public override async Task Run(CheckContext c)
    {
        foreach (var (name, json) in Refused)
        {
            var answer = await c.Twin.InboundJson(json);
            c.Check($"inbound: 400 for {name}", answer.Status == BadRequest && JsonShape.IsError(answer.Json), answer.ToString());
        }

        var accepted = new[]
        {
            ("inbound: 201 for an empty body", await c.Twin.Inbound(Reply(string.Empty))),
            ("inbound: 201 for 1600 characters", await c.Twin.Inbound(Reply(new string('a', 1600)))),
            ("inbound: 201 with an unknown key, ignored", await c.Twin.InboundJson($$"""{"from":"{{A}}","to":"{{Sender}}","body":"extra","x":[1,2]}""")),
            ("inbound: 201 with a key in other capitals, not read", await c.Twin.InboundJson($$"""{"from":"{{A}}","to":"{{Sender}}","body":"lower","Body":"upper"}""")),
        };
        foreach (var (name, answer) in accepted)
        {
            c.Check(name, answer.Status == Created, answer.ToString());
        }

        var list = await c.Twin.InboundList();
        c.Check("inbound: the key in other capitals leaves the right one", list.Any(m => m.Body == "lower") && list.All(m => m.Body != "upper"));
        c.Check("inbound: only the accepted messages were stored", list.Count == accepted.Length, $"{list.Count} messages");
    }
}

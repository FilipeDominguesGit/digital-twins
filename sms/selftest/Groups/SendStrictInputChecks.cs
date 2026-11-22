using System.Text;
using SmsTwin.SelfTest.Checking;
using SmsTwin.SelfTest.Client;
using static System.Net.HttpStatusCode;
using static SmsTwin.SelfTest.TestData;

namespace SmsTwin.SelfTest.Groups;

/// <summary>
/// Locks what the send API refuses and accepts beyond the README's list (D-028): JSON types are never
/// converted, keys are case-sensitive, unknown keys are ignored, and malformed bodies are a 400.
/// </summary>
internal sealed class SendStrictInputChecks : CheckGroup
{
    private static readonly (string Name, string Json)[] Refused =
    [
        ("'to' as a number", $$"""{"to":351910000001,"from":"{{Sender}}","body":"hi"}"""),
        ("'to' as null", $$"""{"to":null,"from":"{{Sender}}","body":"hi"}"""),
        ("'from' as a number", $$"""{"to":"{{A}}","from":5,"body":"hi"}"""),
        ("'from' as null", $$"""{"to":"{{A}}","from":null,"body":"hi"}"""),
        ("'body' as a number", $$"""{"to":"{{A}}","from":"{{Sender}}","body":5}"""),
        ("'body' as true", $$"""{"to":"{{A}}","from":"{{Sender}}","body":true}"""),
        ("'body' as an array", $$"""{"to":"{{A}}","from":"{{Sender}}","body":["hi"]}"""),
        ("'body' as an object", $$$"""{"to":"{{{A}}}","from":"{{{Sender}}}","body":{"text":"hi"}}"""),
        ("key 'To' with a capital", $$"""{"To":"{{A}}","from":"{{Sender}}","body":"hi"}"""),
        ("key 'FROM' in capitals", $$"""{"to":"{{A}}","FROM":"{{Sender}}","body":"hi"}"""),
        ("key 'Body' with a capital", $$"""{"to":"{{A}}","from":"{{Sender}}","Body":"hi"}"""),
        ("a duplicate key", $$"""{"to":"{{A}}","from":"{{Sender}}","body":"hi","to":"{{B}}"}"""),
        ("a lone surrogate escape", $$"""{"to":"{{A}}","from":"{{Sender}}","body":"\ud800"}"""),
        ("a trailing comma", $$"""{"to":"{{A}}","from":"{{Sender}}","body":"hi",}"""),
        ("a comment", $$"""{/* note */"to":"{{A}}","from":"{{Sender}}","body":"hi"}"""),
        ("two JSON values", $$"""{"to":"{{A}}","from":"{{Sender}}","body":"hi"} {}"""),
        ("JSON null", "null"),
        ("a JSON string", "\"hi\""),
        ("a JSON number", "5"),
        ("an empty body", string.Empty),
    ];

    public override string Name => "Send API: strict input";

    public override async Task Run(CheckContext c)
    {
        foreach (var (name, json) in Refused)
        {
            var answer = await c.Twin.SendJson(json);
            c.Check($"send: 400 for {name}", answer.Status == BadRequest && JsonShape.IsError(answer.Json), answer.ToString());
        }

        var invalidUtf8 = await c.Twin.SendBytes(Utf8($$"""{"to":"{{A}}","from":"{{Sender}}","body":"a""", [0xFF], Utf8("b\"}")), "application/json");
        c.Check("send: 400 for invalid UTF-8", invalidUtf8.Status == BadRequest && JsonShape.IsError(invalidUtf8.Json), invalidUtf8.ToString());

        var accepted = 0;
        async Task Accepts(string name, Task<TwinResponse> send)
        {
            var answer = await send;
            accepted += answer.Status == Accepted ? 1 : 0;
            c.Check(name, answer.Status == Accepted, answer.ToString());
        }

        await Accepts("send: an unknown key is ignored", c.Twin.SendJson($$"""{"to":"{{A}}","from":"{{Sender}}","body":"extra","priority":1}"""));
        await Accepts("send: a key with other capitals is not read", c.Twin.SendJson($$"""{"to":"{{A}}","from":"{{Sender}}","body":"lower","Body":"upper"}"""));
        await Accepts("send: Content-Type text/plain is read as JSON", c.Twin.SendBytes(Utf8($$"""{"to":"{{A}}","from":"{{Sender}}","body":"plain"}"""), "text/plain"));
        await Accepts("send: no Content-Type is read as JSON", c.Twin.SendBytes(Utf8($$"""{"to":"{{A}}","from":"{{Sender}}","body":"none"}"""), null));
        await Accepts("send: a UTF-8 byte order mark is skipped", c.Twin.SendBytes(Utf8("﻿" + $$"""{"to":"{{A}}","from":"{{Sender}}","body":"bom"}"""), "application/json"));
        await Accepts("send: an escaped character is read", c.Twin.SendJson($$"""{"to":"{{A}}","from":"{{Sender}}","body":"café 😀"}"""));

        var messages = await c.Twin.Messages();
        c.Check("send: a key with other capitals leaves the right one", messages.Any(m => m.Body == "lower") && messages.All(m => m.Body != "upper"));
        c.Check("send: an escaped character is stored decoded", messages.Any(m => m.Body == "café 😀"));

        var first = await c.Twin.Send(Message("empty key 1"), idempotencyKey: string.Empty);
        var second = await c.Twin.Send(Message("empty key 2"), idempotencyKey: string.Empty);
        accepted += 2;
        var keyless = (await c.Twin.Messages()).Where(m => m.Body.StartsWith("empty key", StringComparison.Ordinal)).ToList();
        c.Check(
            "send: an empty Idempotency-Key counts as none",
            first.Status == Accepted && second.Status == Accepted && keyless.Count == 2 && keyless.All(m => m.IdempotencyKey is null),
            $"{first} {second}");

        var stored = await c.Twin.Messages();
        c.Check("send: only the accepted requests were stored", stored.Count == accepted, $"{stored.Count} messages, {accepted} accepted");
    }

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static byte[] Utf8(string before, byte[] raw, byte[] after) => [.. Utf8(before), .. raw, .. after];
}

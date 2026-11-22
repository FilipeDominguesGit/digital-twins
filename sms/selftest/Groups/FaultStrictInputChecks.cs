using SmsTwin.SelfTest.Checking;
using SmsTwin.SelfTest.Client;
using static System.Net.HttpStatusCode;

namespace SmsTwin.SelfTest.Groups;

/// <summary>
/// Locks what the fault API refuses and accepts beyond the README's table (D-028): numbers are never
/// read from strings or fractions, <c>action</c> is exact, <c>to</c> is a number (0.4.0), keys are case-sensitive,
/// unknown keys are ignored.
/// </summary>
internal sealed class FaultStrictInputChecks : CheckGroup
{
    private static readonly (string Name, string Json)[] Refused =
    [
        ("'times' as a string", """{"action":"error","times":"2"}"""),
        ("'times' as 2.0", """{"action":"error","times":2.0}"""),
        ("'times' as 1.5", """{"action":"error","times":1.5}"""),
        ("'times' as 1e0", """{"action":"error","times":1e0}"""),
        ("'times' negative", """{"action":"error","times":-1}"""),
        ("'times' past a 32-bit integer", """{"action":"error","times":3000000000}"""),
        ("'times' as true", """{"action":"error","times":true}"""),
        ("'times' as null", """{"action":"error","times":null}"""),
        ("'status' as a string", """{"action":"error","times":1,"status":"503"}"""),
        ("'status' as 503.0", """{"action":"error","times":1,"status":503.0}"""),
        ("'status' 99", """{"action":"error","times":1,"status":99}"""),
        ("'delayMs' as a string", """{"action":"delay","times":1,"delayMs":"100"}"""),
        ("'delayMs' as 1.5", """{"action":"delay","times":1,"delayMs":1.5}"""),
        ("'delayMs' negative", """{"action":"delay","times":1,"delayMs":-1}"""),
        ("'delayMs' 120001", """{"action":"delay","times":1,"delayMs":120001}"""),
        ("'delayMs' null for delay", """{"action":"delay","times":1,"delayMs":null}"""),
        ("action 'Error'", """{"action":"Error","times":1}"""),
        ("action 'ERROR'", """{"action":"ERROR","times":1}"""),
        ("action ' error'", """{"action":" error","times":1}"""),
        ("action 'accept_then_error'", """{"action":"accept_then_error","times":1}"""),
        ("action 'AcceptThenError'", """{"action":"AcceptThenError","times":1}"""),
        ("action 'acceptThenError'", """{"action":"acceptThenError","times":1}"""),
        ("action 'error,delay'", """{"action":"error,delay","times":1}"""),
        ("action '1'", """{"action":"1","times":1}"""),
        ("action as a number", """{"action":0,"times":1}"""),
        ("action as null", """{"action":null,"times":1}"""),
        ("action as an array", """{"action":["error"],"times":1}"""),
        ("key 'Action' with a capital", """{"Action":"error","times":1}"""),
        ("key 'Times' with a capital", """{"action":"error","Times":1}"""),
        ("'to' as a number", """{"action":"error","times":1,"to":351910000001}"""),
        ("'to' as true", """{"action":"error","times":1,"to":true}"""),
        ("'to' that is not a number (0.4.0)", """{"action":"error","times":1,"to":"not-a-number"}"""),
        ("'to' empty (0.4.0)", """{"action":"error","times":1,"to":""}"""),
        ("'to' without '+' (0.4.0)", """{"action":"error","times":1,"to":"351910000001"}"""),
        ("'to' of 7 digits (0.4.0)", """{"action":"error","times":1,"to":"+1234567"}"""),
        ("'to' with a trailing newline (0.4.0)", """{"action":"error","times":1,"to":"+351910000001\n"}"""),
        ("'bodyContains' as an object", """{"action":"error","times":1,"bodyContains":{}}"""),
        ("a duplicate key", """{"action":"error","times":1,"times":2}"""),
        ("invalid JSON", "{not json"),
        ("a JSON array", "[]"),
        ("a JSON string", "\"error\""),
        ("a trailing comma", """{"action":"error","times":1,}"""),
        ("an empty body", string.Empty),
    ];

    private static readonly (string Name, string Json)[] Accepted =
    [
        ("'status' 100", """{"action":"error","times":1,"status":100}"""),
        ("'status' 599", """{"action":"error","times":1,"status":599}"""),
        ("'delayMs' 0", """{"action":"delay","times":1,"delayMs":0}"""),
        ("'delayMs' 120000", """{"action":"delay","times":1,"delayMs":120000}"""),
        ("'times' the largest 32-bit integer", """{"action":"error","times":2147483647}"""),
        ("every optional field null", """{"action":"error","times":1,"to":null,"bodyContains":null,"status":null,"delayMs":null}"""),
        ("an unknown key, ignored", """{"action":"error","times":1,"extra":{"a":1}}"""),
        ("'to' of 8 digits", """{"action":"error","times":1,"to":"+12345678"}"""),
        ("'to' of 15 digits", """{"action":"error","times":1,"to":"+123456789012345"}"""),
        ("'bodyContains' of one space", """{"action":"error","times":1,"bodyContains":" "}"""),
        ("'status' with delay, unused", """{"action":"delay","times":1,"delayMs":0,"status":418}"""),
        ("a UTF-8 byte order mark", "﻿{\"action\":\"error\",\"times\":1}"),
    ];

    public override string Name => "Fault API: strict input";

    public override async Task Run(CheckContext c)
    {
        foreach (var (name, json) in Refused)
        {
            var answer = await c.Twin.AddFaultJson(json);
            c.Check($"fault: 400 for {name}", answer.Status == BadRequest && JsonShape.IsError(answer.Json), answer.ToString());
        }

        foreach (var (name, json) in Accepted)
        {
            var answer = await c.Twin.AddFaultJson(json);
            c.Check($"fault: 201 for {name}", answer.Status == Created, answer.ToString());
        }

        var capitalised = await c.Twin.AddFaultJson("""{"action":"error","times":1,"Status":600,"BodyContains":"x"}""");
        var rule = capitalised.As<FaultRuleView>();
        c.Check(
            "fault: keys with other capitals are not read",
            capitalised.Status == Created && rule.Status is null && rule.BodyContains is null,
            capitalised.ToString());

        var rules = await c.Twin.Faults();
        c.Check("fault: only the accepted rules were added", rules.Count == Accepted.Length + 1, $"{rules.Count} rules");
    }
}

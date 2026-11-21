#:property TargetFramework=net10.0
#:property PublishAot=false
#:property Nullable=enable

// Self-test of the SMS twin's contract (README.md), against a running twin.
// Usage: dotnet run selftest.cs -- <base url> <api key> [<webhook secret> <listen prefix>]
// The twin must run with TWIN_API_KEY set to <api key>. The test resets the twin's state.
// With the webhook arguments, the test listens on <listen prefix> (e.g. http://localhost:8091/) and the
// twin must run with TWIN_WEBHOOK_URL pointing there (path /hook) and TWIN_WEBHOOK_SECRET set to <webhook secret>.
// Without them, the twin must run with no TWIN_WEBHOOK_URL.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: dotnet run selftest.cs -- <base url> <api key> [<webhook secret> <listen prefix>]");
    return 2;
}

var baseUrl = args[0].TrimEnd('/');
var key = args[1];
var webhookSecret = args.Length >= 4 ? args[2] : null;
var listenPrefix = args.Length >= 4 ? args[3] : null;
var http = new HttpClient { BaseAddress = new Uri(baseUrl + "/"), Timeout = TimeSpan.FromSeconds(30) };
var passed = 0;
var failed = new List<string>();

void Check(string name, bool ok, string detail = "")
{
    if (ok) passed++;
    else failed.Add(detail.Length > 0 ? $"{name}: {detail}" : name);
}

async Task<(HttpStatusCode Status, JsonNode? Json)> Send(object? payload, string? apiKey = "default", string? idempotencyKey = null, string? raw = null)
{
    var request = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
    {
        Content = raw is not null ? new StringContent(raw, Encoding.UTF8, "application/json") : JsonContent.Create(payload),
    };
    if (apiKey is not null) request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {(apiKey == "default" ? key : apiKey)}");
    if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);
    var response = await http.SendAsync(request);
    var text = await response.Content.ReadAsStringAsync();
    return (response.StatusCode, text.Length > 0 ? JsonNode.Parse(text) : null);
}

async Task<JsonArray> Messages(string? to = null) =>
    (await http.GetFromJsonAsync<JsonArray>(to is null ? "twin/messages" : $"twin/messages?to={Uri.EscapeDataString(to)}"))!;

async Task<HttpStatusCode> AddFault(object rule) => (await http.PostAsJsonAsync("twin/faults", rule)).StatusCode;

async Task Reset()
{
    await http.DeleteAsync("twin/messages");
    await http.DeleteAsync("twin/faults");
}

const string A = "+351910000001";
const string B = "+351910000002";
object Msg(string body, string to = A) => new { to, from = "Twin", body };
string S(JsonNode? n, string p) => n?[p]?.ToString() ?? "";

// Health
var health = await http.GetFromJsonAsync<JsonObject>("health");
Check("health", S(health, "status") == "ok" && S(health, "version").Length > 0, health?.ToJsonString() ?? "");
await Reset();

// Authentication
Check("401 without key", (await Send(Msg("hi"), apiKey: null)).Status == HttpStatusCode.Unauthorized);
Check("401 with a wrong key", (await Send(Msg("hi"), apiKey: "wrong")).Status == HttpStatusCode.Unauthorized);

// Validation
foreach (var to in new[] { "912345678", "+1234567", "+1234567890123456", "+35191 000001", "" })
    Check($"400 for to '{to}'", (await Send(Msg("hi", to))).Status == HttpStatusCode.BadRequest);
Check("400 for empty body", (await Send(Msg(""))).Status == HttpStatusCode.BadRequest);
Check("400 for body over 1600", (await Send(Msg(new string('a', 1601)))).Status == HttpStatusCode.BadRequest);
Check("400 for missing from", (await Send(new { to = A, body = "hi" })).Status == HttpStatusCode.BadRequest);
Check("400 for invalid JSON", (await Send(null, raw: "{not json")).Status == HttpStatusCode.BadRequest);
Check("400 for a JSON array", (await Send(null, raw: "[]")).Status == HttpStatusCode.BadRequest);
Check("8 and 15 digits accepted",
    (await Send(Msg("hi", "+12345678"))).Status == HttpStatusCode.Accepted && (await Send(Msg("hi", "+123456789012345"))).Status == HttpStatusCode.Accepted);
Check("1600 characters accepted", (await Send(Msg(new string('a', 1600)))).Status == HttpStatusCode.Accepted);
Check("nothing stored by rejected requests", (await Messages()).Count == 3, $"{(await Messages()).Count} messages");
await Reset();

// Encoding and segments
async Task Segments(string name, string body, string encoding, int segments)
{
    var (status, json) = await Send(Msg(body));
    Check(name, status == HttpStatusCode.Accepted && S(json, "encoding") == encoding && S(json, "segments") == segments.ToString(),
        $"{status} {json?.ToJsonString()}");
}
await Segments("GSM-7 160 chars, 1 segment", new string('a', 160), "GSM-7", 1);
await Segments("GSM-7 161 chars, 2 segments", new string('a', 161), "GSM-7", 2);
await Segments("GSM-7 306 chars, 2 segments", new string('a', 306), "GSM-7", 2);
await Segments("GSM-7 307 chars, 3 segments", new string('a', 307), "GSM-7", 3);
await Segments("extension char counts 2 (160 septets)", new string('a', 158) + "€", "GSM-7", 1);
await Segments("extension char counts 2 (161 septets)", new string('a', 159) + "€", "GSM-7", 2);
await Segments("GSM accented letters stay GSM-7", "Olé, Müller, Ñaño, où, à Ça", "GSM-7", 1);
await Segments("accented letter outside GSM is UCS-2", "Ñandú", "UCS-2", 1);
await Segments("UCS-2 70 units, 1 segment", new string('a', 69) + "ã", "UCS-2", 1);
await Segments("UCS-2 71 units, 2 segments", new string('a', 70) + "ã", "UCS-2", 2);
await Segments("UCS-2 134 units, 2 segments", new string('a', 133) + "ã", "UCS-2", 2);
await Segments("UCS-2 135 units, 3 segments", new string('a', 134) + "ã", "UCS-2", 3);
await Segments("emoji is two UTF-16 units", new string('a', 68) + "😀", "UCS-2", 1);
await Segments("backtick is not GSM", "a`b", "UCS-2", 1);
await Reset();

// Message shape and inspection
var (st, first) = await Send(Msg("hello A"), idempotencyKey: "k-shape");
await Send(Msg("hello B", B));
var all = await Messages();
var m0 = all[0]!;
Check("202 response shape", st == HttpStatusCode.Accepted && S(first, "id").StartsWith("sms_") && S(first, "status") == "accepted", first?.ToJsonString() ?? "");
Check("message fields", S(m0, "id") == S(first, "id") && S(m0, "to") == A && S(m0, "from") == "Twin" && S(m0, "body") == "hello A"
    && S(m0, "encoding") == "GSM-7" && S(m0, "segments") == "1" && S(m0, "idempotencyKey") == "k-shape"
    && DateTimeOffset.TryParse(S(m0, "receivedAt"), out _), m0.ToJsonString());
Check("messages in order received", all.Count == 2 && S(all[1], "to") == B);
Check("filter by to", (await Messages(B)).Count == 1 && S((await Messages(B))[0], "body") == "hello B");
Check("no idempotency key is null", all[1]!["idempotencyKey"] is null);
await http.DeleteAsync("twin/messages");
Check("DELETE clears messages", (await Messages()).Count == 0);
var (stAfterClear, _) = await Send(Msg("hello A"), idempotencyKey: "k-shape");
Check("DELETE clears idempotency keys", stAfterClear == HttpStatusCode.Accepted && (await Messages()).Count == 1);
await Reset();

// Idempotency
var (s1, r1) = await Send(Msg("same"), idempotencyKey: "k1");
var (s2, r2) = await Send(Msg("same"), idempotencyKey: "k1");
Check("repeated key replays the first response", s1 == HttpStatusCode.Accepted && s2 == HttpStatusCode.Accepted && S(r1, "id") == S(r2, "id"));
Check("repeated key stores nothing new", (await Messages()).Count == 1);
Check("same key, other body: 409", (await Send(Msg("other"), idempotencyKey: "k1")).Status == HttpStatusCode.Conflict);
Check("same key, other number: 409", (await Send(Msg("same", B), idempotencyKey: "k1")).Status == HttpStatusCode.Conflict);
Check("still one message after 409s", (await Messages()).Count == 1);
await Reset();

// Faults: validation
Check("fault: unknown action 400", await AddFault(new { action = "explode", times = 1 }) == HttpStatusCode.BadRequest);
Check("fault: times 0 is 400", await AddFault(new { action = "error", times = 0 }) == HttpStatusCode.BadRequest);
Check("fault: missing times is 400", await AddFault(new { action = "error" }) == HttpStatusCode.BadRequest);
Check("fault: status 600 is 400", await AddFault(new { action = "error", status = 600, times = 1 }) == HttpStatusCode.BadRequest);
Check("fault: delay without delayMs is 400", await AddFault(new { action = "delay", times = 1 }) == HttpStatusCode.BadRequest);
Check("fault: no rules after rejected ones", (await http.GetFromJsonAsync<JsonArray>("twin/faults"))!.Count == 0);

// Faults: error, matched by number, used up
Check("fault added: 201", await AddFault(new { to = B, action = "error", status = 503, times = 2 }) == HttpStatusCode.Created);
Check("fault does not match another number", (await Send(Msg("to A"))).Status == HttpStatusCode.Accepted);
Check("error fault 1 of 2", (await Send(Msg("to B", B))).Status == HttpStatusCode.ServiceUnavailable);
var faultsNow = (await http.GetFromJsonAsync<JsonArray>("twin/faults"))!;
Check("GET faults shows times left", faultsNow.Count == 1 && S(faultsNow[0], "times") == "1", faultsNow.ToJsonString());
Check("error fault 2 of 2", (await Send(Msg("to B", B))).Status == HttpStatusCode.ServiceUnavailable);
Check("fault used up: 202", (await Send(Msg("to B", B))).Status == HttpStatusCode.Accepted);
Check("error stores nothing", (await Messages(B)).Count == 1);
Check("used-up rule is gone", (await http.GetFromJsonAsync<JsonArray>("twin/faults"))!.Count == 0);
await Reset();

// Faults: default status, order of rules
await AddFault(new { action = "error", times = 1 });
await AddFault(new { action = "error", status = 429, times = 1 });
Check("error default status 503", (await Send(Msg("x"))).Status == HttpStatusCode.ServiceUnavailable);
Check("rules apply in creation order", (await Send(Msg("x"))).Status == HttpStatusCode.TooManyRequests);
await Reset();

// Faults: delay
await AddFault(new { action = "delay", delayMs = 1500, times = 1 });
var clock = Stopwatch.StartNew();
var (sd, _) = await Send(Msg("slow"));
Check("delay then handled normally", sd == HttpStatusCode.Accepted && clock.ElapsedMilliseconds >= 1400, $"{sd} after {clock.ElapsedMilliseconds} ms");
await Reset();

// Faults: accept-then-error, and a retry with the same key
await AddFault(new { action = "accept-then-error", times = 1 });
var (sa, _) = await Send(Msg("once"), idempotencyKey: "k-ate");
Check("accept-then-error answers 500", sa == HttpStatusCode.InternalServerError);
Check("accept-then-error stores the message", (await Messages()).Count == 1);
var (sr, rr) = await Send(Msg("once"), idempotencyKey: "k-ate");
Check("retry with the same key: 202, same id", sr == HttpStatusCode.Accepted && S(rr, "id") == S((await Messages())[0], "id"));
Check("retry stores nothing new", (await Messages()).Count == 1);
await AddFault(new { action = "accept-then-error", status = 502, times = 1 });
Check("accept-then-error custom status", (await Send(Msg("twice"))).Status == HttpStatusCode.BadGateway);
await http.DeleteAsync("twin/faults");
Check("DELETE clears faults", (await http.GetFromJsonAsync<JsonArray>("twin/faults"))!.Count == 0);
await Reset();

// Incoming messages
async Task<(HttpStatusCode Status, JsonNode? Json)> Inbound(object payload)
{
    var response = await http.PostAsJsonAsync("twin/inbound", payload);
    var text = await response.Content.ReadAsStringAsync();
    return (response.StatusCode, text.Length > 0 ? JsonNode.Parse(text) : null);
}
async Task<JsonArray> InboundList() => (await http.GetFromJsonAsync<JsonArray>("twin/inbound"))!;
JsonArray Attempts(JsonNode? n) => n?["attempts"] as JsonArray ?? [];
object In(string body, string from = A) => new { from, to = "Twin", body };

await http.DeleteAsync("twin/inbound");
Check("inbound: 400 for bad from", (await Inbound(In("YES", "912345678"))).Status == HttpStatusCode.BadRequest);
Check("inbound: 400 for empty to", (await Inbound(new { from = A, to = "", body = "YES" })).Status == HttpStatusCode.BadRequest);
Check("inbound: 400 for missing body", (await Inbound(new { from = A, to = "Twin" })).Status == HttpStatusCode.BadRequest);
Check("inbound: 400 for body over 1600", (await Inbound(In(new string('a', 1601)))).Status == HttpStatusCode.BadRequest);
Check("inbound: nothing stored by rejected requests", (await InboundList()).Count == 0);

if (webhookSecret is null)
{
    var (sn, nc) = await Inbound(In("YES"));
    var attempts = Attempts(nc);
    Check("inbound without webhook: 201, not-configured", sn == HttpStatusCode.Created && attempts.Count == 1
        && attempts[0]!["status"] is null && S(attempts[0], "error") == "not-configured", nc?.ToJsonString() ?? "");
}
else
{
    var received = new ConcurrentQueue<(byte[] Body, string? Signature, string? ContentType, string Path)>();
    var reply = 204;
    var replyDelayMs = 0;
    var listener = new HttpListener();
    listener.Prefixes.Add(listenPrefix!);
    listener.Start();
    _ = Task.Run(async () =>
    {
        while (listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await listener.GetContextAsync(); } catch { break; }
            using var buffer = new MemoryStream();
            await ctx.Request.InputStream.CopyToAsync(buffer);
            received.Enqueue((buffer.ToArray(), ctx.Request.Headers["X-Sms-Signature"], ctx.Request.ContentType, ctx.Request.Url!.AbsolutePath));
            if (replyDelayMs > 0) await Task.Delay(replyDelayMs);
            ctx.Response.StatusCode = reply;
            try { ctx.Response.Close(); } catch { }
        }
    });
    var secret = Encoding.UTF8.GetBytes(webhookSecret);
    string Sign(byte[] body) => "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(secret, body));

    var (si, mi) = await Inbound(In(" yes\n"));
    var at = Attempts(mi);
    Check("inbound: 201 with id and one attempt", si == HttpStatusCode.Created && S(mi, "id").StartsWith("in_") && at.Count == 1
        && S(at[0], "status") == "204" && at[0]!["error"] is null && DateTimeOffset.TryParse(S(at[0], "at"), out _), mi?.ToJsonString() ?? "");
    Check("inbound: message fields", S(mi, "from") == A && S(mi, "to") == "Twin" && S(mi, "body") == " yes\n"
        && DateTimeOffset.TryParse(S(mi, "receivedAt"), out _), mi?.ToJsonString() ?? "");
    Check("webhook: delivered once, before the 201", received.Count == 1, $"{received.Count} deliveries");
    received.TryDequeue(out var d1);
    var p1 = JsonNode.Parse(d1.Body);
    Check("webhook: path and content type", d1.Path == "/hook" && (d1.ContentType ?? "").StartsWith("application/json"), $"{d1.Path} {d1.ContentType}");
    Check("webhook: payload fields", S(p1, "id") == S(mi, "id") && S(p1, "from") == A && S(p1, "to") == "Twin" && S(p1, "body") == " yes\n"
        && S(p1, "receivedAt") == S(mi, "receivedAt"), p1?.ToJsonString() ?? "");
    Check("webhook: signature is HMAC-SHA256 of the raw body", d1.Signature == Sign(d1.Body), $"{d1.Signature}");
    Check("webhook: signature is lowercase hex", d1.Signature is { Length: 71 } sig && sig[7..].All(c => char.IsAsciiDigit(c) || c is >= 'a' and <= 'f'), $"{d1.Signature}");

    var rd = await http.PostAsync($"twin/inbound/{S(mi, "id")}/redeliver", null);
    var sr2 = rd.StatusCode;
    var mr = JsonNode.Parse(await rd.Content.ReadAsStringAsync());
    received.TryDequeue(out var d2);
    Check("redeliver: 200, two attempts", sr2 == HttpStatusCode.OK && Attempts(mr).Count == 2, mr?.ToJsonString() ?? "");
    Check("redeliver: same bytes and signature", d2.Body is not null && d2.Body.AsSpan().SequenceEqual(d1.Body) && d2.Signature == d1.Signature);
    Check("redeliver: 404 for an unknown id", (await http.PostAsync("twin/inbound/in_unknown/redeliver", null)).StatusCode == HttpStatusCode.NotFound);

    reply = 500;
    var (s5, m5) = await Inbound(In("NO", B));
    await Task.Delay(300);
    Check("webhook 500: recorded, no retry", s5 == HttpStatusCode.Created && Attempts(m5).Count == 1 && S(Attempts(m5)[0], "status") == "500"
        && received.Count == 1, $"{m5?.ToJsonString()} deliveries {received.Count}");
    received.Clear();
    reply = 204;

    var list = await InboundList();
    Check("GET inbound: in order, with attempts", list.Count == 2 && S(list[0], "id") == S(mi, "id") && Attempts(list[0]).Count == 2
        && S(list[1], "from") == B, list.ToJsonString());

    replyDelayMs = 11_000;
    var slow = Stopwatch.StartNew();
    var (st2, mt) = await Inbound(In("late"));
    Check("webhook timeout after 10 s", st2 == HttpStatusCode.Created && Attempts(mt)[0]!["status"] is null && S(Attempts(mt)[0], "error") == "timeout"
        && slow.ElapsedMilliseconds is >= 9_500 and < 11_000, $"{mt?.ToJsonString()} after {slow.ElapsedMilliseconds} ms");
    replyDelayMs = 0;
    await Task.Delay(1_500);
    listener.Stop();
}
Check("DELETE inbound: 204", (await http.DeleteAsync("twin/inbound")).StatusCode == HttpStatusCode.NoContent);
Check("DELETE inbound clears them", (await InboundList()).Count == 0);
Check("inbound does not touch sent messages", (await Messages()).Count == 0);

Console.WriteLine($"{passed}/{passed + failed.Count} passed");
foreach (var f in failed) Console.WriteLine($"FAIL {f}");
return failed.Count == 0 ? 0 : 1;

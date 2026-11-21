#:property TargetFramework=net10.0
#:property PublishAot=false
#:property Nullable=enable

// Self-test of the SMS twin's contract (README.md), against a running twin.
// Usage: dotnet run selftest.cs -- <base url> <api key>
// The twin must run with TWIN_API_KEY set to <api key>. The test resets the twin's state.

using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: dotnet run selftest.cs -- <base url> <api key>");
    return 2;
}

var baseUrl = args[0].TrimEnd('/');
var key = args[1];
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

Console.WriteLine($"{passed}/{passed + failed.Count} passed");
foreach (var f in failed) Console.WriteLine($"FAIL {f}");
return failed.Count == 0 ? 0 : 1;

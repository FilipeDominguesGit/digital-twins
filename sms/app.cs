#:sdk Microsoft.NET.Sdk.Web
#:property TargetFramework=net10.0
#:property PublishAot=false
#:property Nullable=enable

// SMS twin: a behavioural clone of an SMS provider, for tests and previews.
// Send API like a provider's, an inspection API, a fault API, and incoming messages delivered
// to a signed webhook. State lives in memory.
// Contract: README.md. Product-agnostic: it knows nothing about any product.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

const string TwinVersion = "0.2.0";

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
var app = builder.Build();

var apiKey = Environment.GetEnvironmentVariable("TWIN_API_KEY") is { Length: > 0 } k ? k : null;
var state = new TwinState();
var webhook = Webhook.FromEnvironment();

app.MapGet("/health", () => Results.Json(new { status = "ok", version = TwinVersion }));

// ---------- send API ----------

app.MapPost("/v1/messages", async (HttpRequest request) =>
{
    if (apiKey is not null && request.Headers.Authorization.ToString() != $"Bearer {apiKey}")
        return Error(401, "missing or wrong API key");

    var (payload, problem) = await ReadObject(request);
    if (payload is null) return Error(400, problem!);
    var to = Str(payload, "to");
    var from = Str(payload, "from");
    var body = Str(payload, "body");
    if (to is null || !Sms.IsNumber(to)) return Error(400, "'to' must be '+' followed by 8 to 15 digits");
    if (string.IsNullOrEmpty(from)) return Error(400, "'from' must be a non-empty string");
    if (string.IsNullOrEmpty(body)) return Error(400, "'body' must be a non-empty string");
    if (body.Length > Sms.MaxBodyLength) return Error(400, $"'body' is longer than {Sms.MaxBodyLength} characters");

    var key = request.Headers["Idempotency-Key"].ToString();
    var send = new SendRequest(to, from, body, key.Length > 0 ? key : null);

    var fault = state.TakeFault(to);
    if (fault is { Action: "delay" }) await Task.Delay(fault.DelayMs ?? 0);
    if (fault is { Action: "error" }) return Error(fault.Status ?? 503, "fault injected: error");

    var (status, response) = state.Accept(send);
    if (status == 409) return Error(409, "Idempotency-Key reused with a different request");
    if (fault is { Action: "accept-then-error" }) return Error(fault.Status ?? 500, "fault injected: accept-then-error");
    return Results.Json(response, statusCode: 202);
});

// ---------- inspection API ----------

app.MapGet("/twin/messages", (string? to) => Results.Json(state.Messages(to)));
app.MapDelete("/twin/messages", () => { state.ClearMessages(); return Results.NoContent(); });

// ---------- fault API ----------

app.MapPost("/twin/faults", async (HttpRequest request) =>
{
    var (payload, problem) = await ReadObject(request);
    if (payload is null) return Error(400, problem!);
    var action = Str(payload, "action");
    if (action is not ("error" or "delay" or "accept-then-error"))
        return Error(400, "'action' must be 'error', 'delay' or 'accept-then-error'");
    if (payload["to"] is not null && Str(payload, "to") is null) return Error(400, "'to' must be a string");
    var times = Int(payload, "times");
    if (times is null or < 1) return Error(400, "'times' must be a whole number of at least 1");
    var status = Int(payload, "status");
    if (payload["status"] is not null && status is null or < 100 or > 599) return Error(400, "'status' must be a whole number from 100 to 599");
    var delayMs = Int(payload, "delayMs");
    if (payload["delayMs"] is not null && delayMs is null or < 0 or > 120_000) return Error(400, "'delayMs' must be a whole number from 0 to 120000");
    if (action == "delay" && delayMs is null) return Error(400, "'delay' needs 'delayMs'");
    var rule = state.AddFault(Str(payload, "to"), action, status, delayMs, times.Value);
    return Results.Json(rule, statusCode: 201);
});
app.MapGet("/twin/faults", () => Results.Json(state.Faults()));
app.MapDelete("/twin/faults", () => { state.ClearFaults(); return Results.NoContent(); });

// ---------- incoming messages ----------

app.MapPost("/twin/inbound", async (HttpRequest request) =>
{
    var (payload, problem) = await ReadObject(request);
    if (payload is null) return Error(400, problem!);
    var from = Str(payload, "from");
    var to = Str(payload, "to");
    var body = Str(payload, "body");
    if (from is null || !Sms.IsNumber(from)) return Error(400, "'from' must be '+' followed by 8 to 15 digits");
    if (string.IsNullOrEmpty(to)) return Error(400, "'to' must be a non-empty string");
    if (body is null) return Error(400, "'body' must be a string");
    if (body.Length > Sms.MaxBodyLength) return Error(400, $"'body' is longer than {Sms.MaxBodyLength} characters");

    var inbound = state.AddInbound(from, to, body);
    await webhook.Deliver(inbound);
    return Results.Json(inbound.View(), statusCode: 201);
});
app.MapPost("/twin/inbound/{id}/redeliver", async (string id) =>
{
    if (state.FindInbound(id) is not { } inbound) return Error(404, "unknown incoming message");
    await webhook.Deliver(inbound);
    return Results.Json(inbound.View());
});
app.MapGet("/twin/inbound", () => Results.Json(state.InboundViews()));
app.MapDelete("/twin/inbound", () => { state.ClearInbound(); return Results.NoContent(); });

app.Run();

// ---------- helpers ----------

static IResult Error(int status, string message) => Results.Json(new { error = message }, statusCode: status);

static async Task<(JsonObject? Payload, string? Problem)> ReadObject(HttpRequest request)
{
    try
    {
        var node = await JsonNode.ParseAsync(request.Body);
        return node is JsonObject o ? (o, null) : (null, "the body must be a JSON object");
    }
    catch (JsonException)
    {
        return (null, "the body is not valid JSON");
    }
}

static string? Str(JsonObject o, string name) =>
    o[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

static int? Int(JsonObject o, string name) =>
    o[name] is JsonValue v && v.TryGetValue<int>(out var i) ? i : null;

record SendRequest(string To, string From, string Body, string? IdempotencyKey);

record Message(string Id, string To, string From, string Body, string Encoding, int Segments, DateTimeOffset ReceivedAt, string? IdempotencyKey);

record SendResponse(string Id, string Status, string Encoding, int Segments);

record Attempt(DateTimeOffset At, int? Status, string? Error);

record InboundView(string Id, string From, string To, string Body, DateTimeOffset ReceivedAt, List<Attempt> Attempts);

// An incoming message. Payload holds the exact bytes delivered, so a redelivery sends the same bytes.
sealed class Inbound(string id, string from, string to, string body, DateTimeOffset receivedAt)
{
    private readonly List<Attempt> attempts = [];
    public string Id { get; } = id;
    public byte[] Payload { get; } = JsonSerializer.SerializeToUtf8Bytes(
        new { id, from, to, body, receivedAt }, JsonSerializerOptions.Web);

    public void Record(Attempt attempt)
    {
        lock (attempts) attempts.Add(attempt);
    }

    public InboundView View()
    {
        lock (attempts) return new InboundView(Id, from, to, body, receivedAt, [.. attempts]);
    }
}

// Delivers incoming messages to TWIN_WEBHOOK_URL, signed with TWIN_WEBHOOK_SECRET when it is set.
// One attempt per delivery, 10-second timeout, no automatic retry.
sealed class Webhook(Uri? url, byte[]? secret)
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static Webhook FromEnvironment()
    {
        var raw = Environment.GetEnvironmentVariable("TWIN_WEBHOOK_URL");
        Uri? url = null;
        if (!string.IsNullOrEmpty(raw)
            && (!Uri.TryCreate(raw, UriKind.Absolute, out url) || url.Scheme is not ("http" or "https")))
            throw new InvalidOperationException("TWIN_WEBHOOK_URL must be an absolute http or https URL");
        var secret = Environment.GetEnvironmentVariable("TWIN_WEBHOOK_SECRET") is { Length: > 0 } s ? Encoding.UTF8.GetBytes(s) : null;
        return new Webhook(url, secret);
    }

    public static string Signature(byte[] secret, byte[] payload) =>
        "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(secret, payload));

    public async Task Deliver(Inbound inbound)
    {
        if (url is null)
        {
            inbound.Record(new Attempt(DateTimeOffset.UtcNow, null, "not-configured"));
            return;
        }
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new ByteArrayContent(inbound.Payload) };
        request.Content.Headers.ContentType = new("application/json");
        if (secret is not null) request.Headers.TryAddWithoutValidation("X-Sms-Signature", Signature(secret, inbound.Payload));
        Attempt attempt;
        try
        {
            using var response = await Http.SendAsync(request);
            attempt = new Attempt(DateTimeOffset.UtcNow, (int)response.StatusCode, null);
        }
        catch (TaskCanceledException)
        {
            attempt = new Attempt(DateTimeOffset.UtcNow, null, "timeout");
        }
        catch (HttpRequestException)
        {
            attempt = new Attempt(DateTimeOffset.UtcNow, null, "connection");
        }
        inbound.Record(attempt);
    }
}

record FaultRule(string Id, string? To, string Action, int? Status, int? DelayMs, int Times);

sealed class TwinState
{
    private readonly object gate = new();
    private readonly List<Message> messages = [];
    private readonly Dictionary<string, (SendRequest Request, SendResponse Response)> keys = [];
    private readonly List<FaultRule> faults = [];
    private readonly List<Inbound> inbound = [];

    public Inbound AddInbound(string from, string to, string body)
    {
        var message = new Inbound($"in_{Guid.NewGuid():N}", from, to, body, DateTimeOffset.UtcNow);
        lock (gate) inbound.Add(message);
        return message;
    }

    public Inbound? FindInbound(string id)
    {
        lock (gate) return inbound.FirstOrDefault(m => m.Id == id);
    }

    public List<InboundView> InboundViews()
    {
        lock (gate) return inbound.Select(m => m.View()).ToList();
    }

    public void ClearInbound()
    {
        lock (gate) inbound.Clear();
    }

    // Stores the message, or replays the first response for a repeated Idempotency-Key.
    public (int Status, SendResponse? Response) Accept(SendRequest send)
    {
        lock (gate)
        {
            if (send.IdempotencyKey is { } key && keys.TryGetValue(key, out var seen))
            {
                var same = seen.Request.To == send.To && seen.Request.From == send.From && seen.Request.Body == send.Body;
                return same ? (202, seen.Response) : (409, null);
            }
            var (encoding, segments) = Sms.Count(send.Body);
            var message = new Message($"sms_{Guid.NewGuid():N}", send.To, send.From, send.Body, encoding, segments, DateTimeOffset.UtcNow, send.IdempotencyKey);
            messages.Add(message);
            var response = new SendResponse(message.Id, "accepted", encoding, segments);
            if (send.IdempotencyKey is { } k) keys[k] = (send, response);
            return (202, response);
        }
    }

    public List<Message> Messages(string? to)
    {
        lock (gate) return messages.Where(m => to is null || m.To == to).ToList();
    }

    // Clears the messages and the idempotency keys they were stored under.
    public void ClearMessages()
    {
        lock (gate) { messages.Clear(); keys.Clear(); }
    }

    public FaultRule AddFault(string? to, string action, int? status, int? delayMs, int times)
    {
        lock (gate)
        {
            var rule = new FaultRule($"fault_{Guid.NewGuid():N}", to, action, status, delayMs, times);
            faults.Add(rule);
            return rule;
        }
    }

    // The first rule (in creation order) that matches this number; one use of its 'times'.
    public FaultRule? TakeFault(string to)
    {
        lock (gate)
        {
            var i = faults.FindIndex(f => f.To is null || f.To == to);
            if (i < 0) return null;
            var rule = faults[i];
            if (rule.Times <= 1) faults.RemoveAt(i); else faults[i] = rule with { Times = rule.Times - 1 };
            return rule;
        }
    }

    public List<FaultRule> Faults()
    {
        lock (gate) return [.. faults];
    }

    public void ClearFaults()
    {
        lock (gate) faults.Clear();
    }
}

static partial class Sms
{
    public const int MaxBodyLength = 1600;

    // GSM 03.38 basic character set (without the escape character) and its extension table.
    private const string Basic =
        "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà";
    private const string Extension = "\f^{}\\[~]|€";

    [GeneratedRegex(@"^\+[0-9]{8,15}$")]
    private static partial Regex NumberPattern();

    public static bool IsNumber(string s) => NumberPattern().IsMatch(s);

    // Encoding and segment count as a provider bills them: GSM-7 when every character is in the
    // GSM 03.38 tables (extension characters take two septets), otherwise UCS-2 in UTF-16 code units.
    public static (string Encoding, int Segments) Count(string body)
    {
        var septets = 0;
        foreach (var c in body)
        {
            if (Basic.Contains(c)) septets += 1;
            else if (Extension.Contains(c)) septets += 2;
            else return ("UCS-2", Segments(body.Length, 70, 67));
        }
        return ("GSM-7", Segments(septets, 160, 153));
    }

    private static int Segments(int units, int single, int perPart) =>
        units <= single ? 1 : (units + perPart - 1) / perPart;
}

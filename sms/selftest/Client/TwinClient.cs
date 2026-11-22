using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmsTwin.SelfTest.Client;

/// <summary>
/// The twin's HTTP API, typed. The <c>...Json</c> methods send a body exactly as written,
/// for the checks that are about the shape of the input.
/// </summary>
internal sealed class TwinClient(HttpClient http, string apiKey)
{
    private static readonly JsonSerializerOptions RequestJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private string Authorization => $"Bearer {apiKey}";

    public Task<TwinResponse> Health() => Get("health");

    // ---------- send API ----------
    public Task<TwinResponse> Send(SmsRequest message, string? idempotencyKey = null) =>
        PostSend(JsonContent.Create(message, options: RequestJson), Authorization, idempotencyKey);

    /// <summary>Sends with this <c>Authorization</c> header, or with none when it is null.</summary>
    public Task<TwinResponse> SendWithAuthorization(SmsRequest message, string? authorization) =>
        PostSend(JsonContent.Create(message, options: RequestJson), authorization, null);

    public Task<TwinResponse> SendJson(string json, string? idempotencyKey = null) =>
        PostSend(Json(json), Authorization, idempotencyKey);

    /// <summary>Sends a body exactly as written, with no <c>Authorization</c> header.</summary>
    public Task<TwinResponse> SendJsonWithoutKey(string json) => PostSend(Json(json), null, null);

    /// <summary>Sends these bytes with this content type, or with no <c>Content-Type</c> when it is null.</summary>
    public Task<TwinResponse> SendBytes(byte[] body, string? contentType)
    {
        var content = new ByteArrayContent(body);
        if (contentType is not null)
        {
            content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        }

        return PostSend(content, Authorization, null);
    }

    // ---------- test API: messages ----------
    public async Task<IReadOnlyList<SmsMessage>> Messages(string? to = null) =>
        (await Get(to is null ? "twin/messages" : $"twin/messages?to={Uri.EscapeDataString(to)}")).As<List<SmsMessage>>();

    /// <summary>GET /twin/messages with this query string, as written.</summary>
    public Task<TwinResponse> MessagesWithQuery(string query) => Get($"twin/messages{query}");

    public Task<TwinResponse> ClearMessages() => Delete("twin/messages");

    // ---------- test API: faults ----------
    public Task<TwinResponse> AddFault(FaultSpec rule) => Post("twin/faults", JsonContent.Create(rule, options: RequestJson));

    public Task<TwinResponse> AddFaultJson(string json) => Post("twin/faults", Json(json));

    public async Task<IReadOnlyList<FaultRuleView>> Faults() => (await FaultsResponse()).As<List<FaultRuleView>>();

    public Task<TwinResponse> FaultsResponse() => Get("twin/faults");

    public Task<TwinResponse> ClearFaults() => Delete("twin/faults");

    // ---------- test API: incoming messages ----------
    public Task<TwinResponse> Inbound(InboundRequest message) =>
        Post("twin/inbound", JsonContent.Create(message, options: RequestJson));

    public Task<TwinResponse> InboundJson(string json) => Post("twin/inbound", Json(json));

    public async Task<IReadOnlyList<InboundView>> InboundList() => (await InboundResponse()).As<List<InboundView>>();

    public Task<TwinResponse> InboundResponse() => Get("twin/inbound");

    public Task<TwinResponse> Redeliver(string id) => Post($"twin/inbound/{id}/redeliver", null);

    public Task<TwinResponse> ClearInbound() => Delete("twin/inbound");

    /// <summary>Clears the messages (and idempotency keys), the fault rules and the incoming messages.</summary>
    public async Task Reset()
    {
        foreach (var answer in new[] { await ClearMessages(), await ClearFaults(), await ClearInbound() })
        {
            if (answer.Status != HttpStatusCode.NoContent)
            {
                throw new InvalidOperationException($"reset failed: {answer}");
            }
        }
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static async Task<TwinResponse> Answer(HttpResponseMessage response)
    {
        using (response)
        {
            return new TwinResponse(response.StatusCode, await response.Content.ReadAsStringAsync());
        }
    }

    private async Task<TwinResponse> PostSend(HttpContent content, string? authorization, string? idempotencyKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/messages") { Content = content };
        if (authorization is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        if (idempotencyKey is not null)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        return await Answer(await http.SendAsync(request));
    }

    private async Task<TwinResponse> Get(string path) => await Answer(await http.GetAsync(path));

    private async Task<TwinResponse> Post(string path, HttpContent? content) => await Answer(await http.PostAsync(path, content));

    private async Task<TwinResponse> Delete(string path) => await Answer(await http.DeleteAsync(path));
}

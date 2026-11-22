using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace SmsTwin.Inbound;

/// <summary>
/// Delivers incoming messages to <c>TWIN_WEBHOOK_URL</c>, signed with <c>TWIN_WEBHOOK_SECRET</c> when it is set.
/// One attempt per delivery, a 10-second timeout, no automatic retry.
/// </summary>
internal sealed class Webhook(Uri? url, byte[]? secret)
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>Reads the settings; an invalid <c>TWIN_WEBHOOK_URL</c> stops the twin at startup.</summary>
    public static Webhook FromEnvironment()
    {
        var raw = Environment.GetEnvironmentVariable("TWIN_WEBHOOK_URL");
        Uri? url = null;
        if (!string.IsNullOrEmpty(raw)
            && (!Uri.TryCreate(raw, UriKind.Absolute, out url) || url.Scheme is not ("http" or "https")))
        {
            throw new InvalidOperationException("TWIN_WEBHOOK_URL must be an absolute http or https URL");
        }

        var secret = Environment.GetEnvironmentVariable("TWIN_WEBHOOK_SECRET") is { Length: > 0 } s ? Encoding.UTF8.GetBytes(s) : null;
        return new Webhook(url, secret);
    }

    /// <summary><c>sha256=</c> and the lowercase hex HMAC-SHA256 of the payload bytes.</summary>
    public static string Signature(byte[] secret, byte[] payload) =>
        "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(secret, payload));

    public async Task Deliver(IncomingMessage message)
    {
        if (url is null)
        {
            message.Record(new DeliveryAttempt(DateTimeOffset.UtcNow, null, "not-configured"));
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new ByteArrayContent(message.Payload) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        if (secret is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Sms-Signature", Signature(secret, message.Payload));
        }

        DeliveryAttempt attempt;
        try
        {
            using var response = await Http.SendAsync(request);
            attempt = new DeliveryAttempt(DateTimeOffset.UtcNow, (int)response.StatusCode, null);
        }
        catch (TaskCanceledException)
        {
            attempt = new DeliveryAttempt(DateTimeOffset.UtcNow, null, "timeout");
        }
        catch (HttpRequestException)
        {
            attempt = new DeliveryAttempt(DateTimeOffset.UtcNow, null, "connection");
        }

        message.Record(attempt);
    }
}

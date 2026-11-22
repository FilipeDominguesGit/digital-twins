using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace SmsTwin.SelfTest.Webhook;

/// <summary>
/// A local HTTP listener that plays the product's webhook: it records every delivery,
/// answers with <see cref="Reply"/> after <see cref="ReplyDelayMs"/>, and handles deliveries concurrently.
/// </summary>
internal sealed class WebhookListener : IAsyncDisposable
{
    private readonly HttpListener listener = new();
    private readonly ConcurrentQueue<WebhookDelivery> received = new();
    private readonly byte[] secret;
    private readonly Task loop;
    private volatile int reply = 204;
    private volatile int replyDelayMs;

    private WebhookListener(string listenPrefix, string secret)
    {
        this.secret = Encoding.UTF8.GetBytes(secret);
        listener.Prefixes.Add(listenPrefix);
        listener.Start();
        loop = Task.Run(Listen);
    }

    public int Reply
    {
        get => reply;
        set => reply = value;
    }

    public int ReplyDelayMs
    {
        get => replyDelayMs;
        set => replyDelayMs = value;
    }

    public int Count => received.Count;

    public static WebhookListener Start(string listenPrefix, string secret) => new(listenPrefix, secret);

    public bool TryTake(out WebhookDelivery delivery) => received.TryDequeue(out delivery!);

    /// <summary>Forgets the deliveries so far and goes back to answering 204 at once.</summary>
    public void Clear()
    {
        received.Clear();
        Reply = 204;
        ReplyDelayMs = 0;
    }

    /// <summary>The signature the twin must send for these body bytes.</summary>
    public string Sign(byte[] body) => "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(secret, body));

    public async ValueTask DisposeAsync()
    {
        listener.Stop();
        listener.Close();
        await loop;
    }

    private async Task Listen()
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                break;
            }

            _ = Task.Run(() => Handle(context));
        }
    }

    private async Task Handle(HttpListenerContext context)
    {
        using var buffer = new MemoryStream();
        await context.Request.InputStream.CopyToAsync(buffer);
        received.Enqueue(new WebhookDelivery(
            buffer.ToArray(),
            context.Request.Headers["X-Sms-Signature"],
            context.Request.ContentType,
            context.Request.Url!.AbsolutePath));
        if (ReplyDelayMs > 0)
        {
            await Task.Delay(ReplyDelayMs);
        }

        try
        {
            context.Response.StatusCode = Reply;
            context.Response.Close();
        }
        catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or InvalidOperationException)
        {
            // The twin gave up waiting (the timeout check), or the listener stopped.
        }
    }
}

namespace SmsTwin.SelfTest;

/// <summary>The command line: the twin's base URL, its API key and, optionally, the webhook to listen on.</summary>
internal sealed record SelfTestOptions(Uri BaseUrl, string ApiKey, WebhookOptions? Webhook)
{
    public static SelfTestOptions? Parse(string[] args)
    {
        if (args.Length < 2 || !Uri.TryCreate(args[0].TrimEnd('/') + "/", UriKind.Absolute, out var baseUrl))
        {
            return null;
        }

        var webhook = args.Length >= 4 ? new WebhookOptions(args[2], args[3]) : null;
        return new SelfTestOptions(baseUrl, args[1], webhook);
    }
}

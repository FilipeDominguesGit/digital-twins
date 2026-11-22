// Self-test of the SMS twin's contract (../README.md), against a running twin.
// Usage: dotnet run --project sms/selftest -- <base url> <api key> [<webhook secret> <listen prefix>]
// The twin must run with TWIN_API_KEY set to <api key>. Each check group resets the twin's state first.
// With the webhook arguments, the test listens on <listen prefix> (e.g. http://localhost:8091/) and the
// twin must run with TWIN_WEBHOOK_URL pointing there (path /hook) and TWIN_WEBHOOK_SECRET set to <webhook secret>.
// Without them, the twin must run with no TWIN_WEBHOOK_URL.
using SmsTwin.SelfTest;
using SmsTwin.SelfTest.Checking;
using SmsTwin.SelfTest.Client;
using SmsTwin.SelfTest.Webhook;

if (SelfTestOptions.Parse(args) is not { } options)
{
    Console.Error.WriteLine("Usage: dotnet run --project sms/selftest -- <base url> <api key> [<webhook secret> <listen prefix>]");
    return 2;
}

using var http = new HttpClient { BaseAddress = options.BaseUrl, Timeout = TimeSpan.FromSeconds(30) };
var twin = new TwinClient(http, options.ApiKey);
await using var webhook = options.Webhook is { } w ? WebhookListener.Start(w.ListenPrefix, w.Secret) : null;
var checker = new Checker();

foreach (var group in CheckGroups.For(withWebhook: webhook is not null))
{
    await twin.Reset();
    webhook?.Clear();
    checker.Begin(group.Name);
    try
    {
        await group.Run(new CheckContext(twin, webhook, checker));
    }
    catch (Exception e)
    {
        checker.Crash(e);
    }
}

checker.Report(Console.Out);
return checker.FailedCount == 0 ? 0 : 1;

namespace SmsTwin.SelfTest.Checking;

/// <summary>Whether a group runs always, only with the webhook arguments, or only without them.</summary>
internal enum GroupMode
{
    Always,
    WithWebhook,
    WithoutWebhook,
}

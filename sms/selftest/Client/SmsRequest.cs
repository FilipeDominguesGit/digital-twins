namespace SmsTwin.SelfTest.Client;

/// <summary>A send request: <c>POST /v1/messages</c>.</summary>
internal sealed record SmsRequest(string To, string From, string Body);

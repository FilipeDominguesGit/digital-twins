namespace SmsTwin.SelfTest.Client;

/// <summary>A simulated incoming SMS: <c>POST /twin/inbound</c>.</summary>
internal sealed record InboundRequest(string From, string To, string Body);

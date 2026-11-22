namespace SmsTwin.SelfTest.Client;

/// <summary>The 202 answer of the send API.</summary>
internal sealed record SendResult(string Id, string Status, string Encoding, int Segments);

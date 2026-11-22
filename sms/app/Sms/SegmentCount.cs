namespace SmsTwin.Sms;

/// <summary>How a provider bills a text: <c>GSM-7</c> or <c>UCS-2</c>, and the number of segments.</summary>
internal sealed record SegmentCount(string Encoding, int Segments);

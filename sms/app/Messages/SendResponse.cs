namespace SmsTwin.Messages;

/// <summary>The 202 answer of the send API.</summary>
internal sealed record SendResponse(string Id, string Status, string Encoding, int Segments)
{
    public static SendResponse For(Message message) => new(message.Id, "accepted", message.Encoding, message.Segments);
}

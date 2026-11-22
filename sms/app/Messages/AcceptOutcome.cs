namespace SmsTwin.Messages;

internal enum AcceptOutcome
{
    /// <summary>A new message was stored.</summary>
    Stored,

    /// <summary>The idempotency key was seen with the same send; the first message stands.</summary>
    Replayed,

    /// <summary>The idempotency key was seen with a different send; nothing was stored.</summary>
    Conflict,
}

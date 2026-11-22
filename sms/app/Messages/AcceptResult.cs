using System.Diagnostics.CodeAnalysis;

namespace SmsTwin.Messages;

/// <summary>What <see cref="MessageStore.Accept"/> did, and the message the answer is about.</summary>
internal sealed record AcceptResult(AcceptOutcome Outcome, Message? Message)
{
    [MemberNotNullWhen(false, nameof(Message))]
    public bool IsConflict => Outcome == AcceptOutcome.Conflict;
}

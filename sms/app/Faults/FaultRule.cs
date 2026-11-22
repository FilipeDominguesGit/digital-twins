namespace SmsTwin.Faults;

/// <summary>A fault rule, with the <c>times</c> it has left.</summary>
internal sealed record FaultRule(string Id, string? To, string? BodyContains, FaultAction Action, int? Status, int? DelayMs, int Times)
{
    /// <summary>True when the rule's number (if any) and its text (if any; ordinal, case-sensitive) match this send.</summary>
    public bool Matches(string to, string body) =>
        (To is null || To == to) && (BodyContains is null || body.Contains(BodyContains, StringComparison.Ordinal));
}

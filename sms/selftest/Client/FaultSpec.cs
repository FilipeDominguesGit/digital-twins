namespace SmsTwin.SelfTest.Client;

/// <summary>A fault rule as sent to <c>POST /twin/faults</c>. Null fields are left out of the JSON.</summary>
internal sealed record FaultSpec(
    string Action,
    int? Times = null,
    string? To = null,
    string? BodyContains = null,
    int? Status = null,
    int? DelayMs = null);

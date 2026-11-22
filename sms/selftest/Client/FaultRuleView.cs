namespace SmsTwin.SelfTest.Client;

/// <summary>A fault rule, as the twin shows it.</summary>
internal sealed record FaultRuleView(string Id, string? To, string? BodyContains, string Action, int? Status, int? DelayMs, int Times);

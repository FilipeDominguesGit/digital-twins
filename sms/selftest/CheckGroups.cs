using SmsTwin.SelfTest.Checking;
using SmsTwin.SelfTest.Groups;

namespace SmsTwin.SelfTest;

/// <summary>Every check group, in the order they run.</summary>
internal static class CheckGroups
{
    private static readonly CheckGroup[] All =
    [
        new HealthChecks(),
        new AuthenticationChecks(),
        new SendValidationChecks(),
        new SendStrictInputChecks(),
        new SendOrderChecks(),
        new EncodingChecks(),
        new MessageInspectionChecks(),
        new IdempotencyChecks(),
        new FaultValidationChecks(),
        new FaultStrictInputChecks(),
        new ErrorFaultChecks(),
        new FaultOrderChecks(),
        new BodyContainsChecks(),
        new ToAndBodyContainsChecks(),
        new SameNumberFaultChecks(),
        new DelayFaultChecks(),
        new AcceptThenErrorChecks(),
        new InboundValidationChecks(),
        new InboundStrictInputChecks(),
        new InboundWithoutWebhookChecks(),
        new WebhookDeliveryChecks(),
        new RedeliveryChecks(),
        new WebhookFailureChecks(),
        new WebhookTimeoutChecks(),
        new InboundClearChecks(),
        new ResponseShapeChecks(),
    ];

    public static IEnumerable<CheckGroup> For(bool withWebhook) =>
        All.Where(g => g.Mode == GroupMode.Always || g.Mode == (withWebhook ? GroupMode.WithWebhook : GroupMode.WithoutWebhook));
}

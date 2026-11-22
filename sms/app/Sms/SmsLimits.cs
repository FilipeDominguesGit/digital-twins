namespace SmsTwin.Sms;

internal static class SmsLimits
{
    /// <summary>The longest body, in UTF-16 code units, for sends and incoming messages.</summary>
    public const int MaxBodyLength = 1600;
}

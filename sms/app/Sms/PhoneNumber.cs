using System.Text.RegularExpressions;

namespace SmsTwin.Sms;

internal static partial class PhoneNumber
{
    /// <summary>True for <c>+</c> followed by 8 to 15 digits.</summary>
    public static bool IsValid(string number) => Pattern().IsMatch(number);

    [GeneratedRegex(@"^\+[0-9]{8,15}$")]
    private static partial Regex Pattern();
}

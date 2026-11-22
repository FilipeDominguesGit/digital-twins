namespace SmsTwin.Sms;

internal static class GsmEncoding
{
    // GSM 03.38 basic character set (without the escape character) and its extension table.
    private const string Basic =
        "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà";
    private const string Extension = "\f^{}\\[~]|€";

    /// <summary>
    /// The encoding and segment count as a provider bills them: GSM-7 when every character is in the
    /// GSM 03.38 tables (extension characters take two septets), otherwise UCS-2 in UTF-16 code units.
    /// </summary>
    public static SegmentCount Count(string body)
    {
        var septets = 0;
        foreach (var c in body)
        {
            if (Basic.Contains(c))
            {
                septets += 1;
            }
            else if (Extension.Contains(c))
            {
                septets += 2;
            }
            else
            {
                return new SegmentCount("UCS-2", Segments(body.Length, 70, 67));
            }
        }

        return new SegmentCount("GSM-7", Segments(septets, 160, 153));
    }

    private static int Segments(int units, int single, int perPart) =>
        units <= single ? 1 : (units + perPart - 1) / perPart;
}

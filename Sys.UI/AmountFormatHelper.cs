using System.Globalization;
using System.Text;
using Avalonia.Controls;

namespace Sys.UI;

public static class AmountFormatHelper
{
    public static void Format(object? sender)
    {
        if (sender is not TextBox tb || string.IsNullOrWhiteSpace(tb.Text)) return;
        if (decimal.TryParse(tb.Text, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out var value))
            tb.Text = value.ToString("N2", CultureInfo.GetCultureInfo("tr-TR"));
    }

    public static void FormatLive(object? sender)
    {
        if (sender is not TextBox tb) return;
        var text = tb.Text ?? string.Empty;
        var caret = tb.CaretIndex;

        var digitsBeforeCaret = CountDigits(text, caret);
        var cleaned = CleanForLiveFormat(text);
        var formatted = FormatWithThousands(cleaned);

        if (formatted == text) return; // değişiklik yoksa dokunma, sonsuz döngü olmasın

        tb.Text = formatted;
        tb.CaretIndex = FindCaretIndexByDigitCount(formatted, digitsBeforeCaret);
    }

    private static int CountDigits(string text, int uptoIndex)
    {
        var count = 0;
        for (var i = 0; i < uptoIndex && i < text.Length; i++)
            if (char.IsDigit(text[i])) count++;
        return count;
    }

    private static int FindCaretIndexByDigitCount(string text, int digitCount)
    {
        if (digitCount <= 0) return 0;
        var seen = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsDigit(text[i]))
            {
                seen++;
                if (seen == digitCount) return i + 1;
            }
        }
        return text.Length;
    }

    private static string CleanForLiveFormat(string text)
    {
        var sb = new StringBuilder();
        foreach (var c in text)
            if (char.IsDigit(c) || c == ',' || c == '-')
                sb.Append(c);
        return sb.ToString();
    }

    private static string FormatWithThousands(string cleaned)
    {
        var negative = cleaned.StartsWith("-");
        if (negative) cleaned = cleaned[1..];

        var parts = cleaned.Split(',');
        var integerPart = parts[0];
        var fractionPart = parts.Length > 1 ? parts[1] : null;

        if (integerPart.Length == 0)
            return (negative ? "-" : "") + (fractionPart != null ? "," + fractionPart : "");

        var grouped = GroupThousands(integerPart);
        var result = (negative ? "-" : "") + grouped;
        if (fractionPart != null) result += "," + fractionPart;
        return result;
    }

    private static string GroupThousands(string digits)
    {
        if (digits.Length <= 3) return digits;
        var sb = new StringBuilder();
        var offset = digits.Length % 3;
        if (offset > 0) { sb.Append(digits, 0, offset); sb.Append('.'); }
        for (var i = offset; i < digits.Length; i += 3)
        {
            sb.Append(digits, i, 3);
            if (i + 3 < digits.Length) sb.Append('.');
        }
        return sb.ToString();
    }
}
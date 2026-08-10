using System.Globalization;
using Avalonia.Controls;

namespace Sys.UI;

public static class AmountFormatHelper
{
    public static void Format(object? sender)
    {
        if (sender is not TextBox tb || string.IsNullOrWhiteSpace(tb.Text)) return;

        if (decimal.TryParse(tb.Text, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out var value))
        {
            tb.Text = value.ToString("N2", CultureInfo.GetCultureInfo("tr-TR"));
        }
    }
}
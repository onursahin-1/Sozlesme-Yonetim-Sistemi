using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Sys.UI.ViewModels;

// 0..1 aralığındaki bir oranı piksel genişliğine çevirir.
//
// Gösterge panelindeki tür dağılımı çubukları için: her satırın değeri en yüksek
// değere oranlanıp (ViewModel'de) buraya geliyor, burada da ConverterParameter ile
// verilen azami genişlikle çarpılıyor. Böylece çubuk mantığı XAML'de tek satır kalıyor.
public class RatioToWidthConverter : IValueConverter
{
    public static readonly RatioToWidthConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var ratio = value switch
        {
            double d => d,
            float f => f,
            _ => 0d
        };

        var maxWidth = parameter switch
        {
            string s when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) => p,
            double d => d,
            _ => 200d
        };

        if (double.IsNaN(ratio) || ratio <= 0) return 0d;
        if (ratio > 1) ratio = 1;

        // Sıfır olmayan ama çok küçük değerler tamamen görünmez olmasın diye alt sınır.
        return Math.Max(4d, ratio * maxWidth);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

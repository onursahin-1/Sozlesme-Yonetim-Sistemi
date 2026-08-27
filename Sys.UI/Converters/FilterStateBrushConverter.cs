using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Sys.UI.Converters;

// SelectedFilter (string) değerini ConverterParameter'daki filtre anahtarıyla karşılaştırır;
// eşleşiyorsa "aktif" rengini, eşleşmiyorsa "pasif" rengini döner. Sözleşme listesindeki
// filtre butonlarında hangi filtrenin seçili olduğunu görsel olarak belirtmek için kullanılır.
public class FilterStateBrushConverter : IValueConverter
{
    public static readonly FilterStateBrushConverter Background = new("AccentBase", "SurfaceDivider");
    public static readonly FilterStateBrushConverter Foreground = new("TextOnAccent", "TextBody");

    // Renkler artık hazır fırça değil PALET ANAHTARI. Eskiden kurucuda bir kez
    // SolidColorBrush üretiliyordu; o fırça tema değişse de aynı kalırdı.
    private readonly string _activeKey;
    private readonly string _inactiveKey;

    private FilterStateBrushConverter(string activeKey, string inactiveKey)
    {
        _activeKey = activeKey;
        _inactiveKey = inactiveKey;
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is string s && parameter is string p && s == p ? _activeKey : _inactiveKey;
        return ThemeBrushConverter.Instance.Convert(key, targetType, null, culture);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
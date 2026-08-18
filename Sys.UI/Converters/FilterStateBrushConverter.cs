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
    public static readonly FilterStateBrushConverter Background = new("#2D6EA8", "#E5E7EB");
    public static readonly FilterStateBrushConverter Foreground = new("White", "#374151");

    private readonly IBrush _active;
    private readonly IBrush _inactive;

    private FilterStateBrushConverter(string activeColor, string inactiveColor)
    {
        _active = new SolidColorBrush(Color.Parse(activeColor));
        _inactive = new SolidColorBrush(Color.Parse(inactiveColor));
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && parameter is string p && s == p ? _active : _inactive;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
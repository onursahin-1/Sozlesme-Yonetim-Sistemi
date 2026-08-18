using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Sys.UI.Converters;

// Bool bir değeri (örn. IsStep1) true/false renklerine çevirir. Sihirbazdaki
// adım göstergesinde aktif adımı görsel olarak vurgulamak için kullanılır.
public class BoolBrushConverter : IValueConverter
{
    public static readonly BoolBrushConverter StepIndicator = new("#2D6EA8", "#9CA3AF");

    private readonly IBrush _true;
    private readonly IBrush _false;

    private BoolBrushConverter(string trueColor, string falseColor)
    {
        _true = new SolidColorBrush(Color.Parse(trueColor));
        _false = new SolidColorBrush(Color.Parse(falseColor));
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? _true : _false;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
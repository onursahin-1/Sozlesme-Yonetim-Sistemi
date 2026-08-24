using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Sys.UI.Converters;

// bool -> renk. Şifre kuralları listesinde her kuralın sağlanıp sağlanmadığını
// göstermek için kullanılıyor: sağlananlar yeşil, sağlanmayanlar soluk gri.
//
// Kuralı gizlemek yerine soluk göstermek bilinçli: kullanıcının kaç kural kaldığını
// görmesi gerekiyor. Kurallar yazdıkça listeden kaybolsaydı, eksik olanın ne
// olduğunu değil yalnızca "bir şeyler eksik" olduğunu bilirdi.
public class BoolToBrushConverter : IValueConverter
{
    public static readonly BoolToBrushConverter Rule = new("#1A6B2A", "#B6BDC9");

    private readonly IBrush _true;
    private readonly IBrush _false;

    private BoolToBrushConverter(string trueColor, string falseColor)
    {
        _true = new SolidColorBrush(Color.Parse(trueColor));
        _false = new SolidColorBrush(Color.Parse(falseColor));
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? _true : _false;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

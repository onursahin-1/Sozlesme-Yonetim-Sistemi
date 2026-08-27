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
    public static readonly BoolToBrushConverter Rule = new("SuccessBase", "TextDisabled");

    // Palet anahtarları; gerçek fırça her çağrıda o anki temadan çözülüyor.
    private readonly string _trueKey;
    private readonly string _falseKey;

    private BoolToBrushConverter(string trueKey, string falseKey)
    {
        _trueKey = trueKey;
        _falseKey = falseKey;
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => ThemeBrushConverter.Instance.Convert(value is true ? _trueKey : _falseKey, targetType, null, culture);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

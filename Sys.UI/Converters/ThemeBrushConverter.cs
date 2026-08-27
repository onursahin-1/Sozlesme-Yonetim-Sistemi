using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Sys.UI.Converters;

// ViewModel'lerin ürettiği renkleri temaya bağlar.
//
// Sorun: durum rozetlerinin rengi (Aktif yeşil, İhlal kırmızı…) XAML'de değil
// ViewModel'de belirleniyor — çünkü karar iş kuralına ait, görünüme değil.
// Bu renkler eskiden ham hex olarak dönüyordu ("DangerBase") ve doğrudan Foreground'a
// bağlanıyordu; dolayısıyla tema değişse de aynı kalıyorlardı.
//
// Çözüm: ViewModel artık rengi değil PALETTEKİ ROLÜN ADINI döndürüyor
// ("DangerBase"). Bu dönüştürücü o adı, o anki temanın sözlüğünden gerçek fırçaya
// çeviriyor. Böylece renk değerleri tek yerde — Themes/Palette.axaml içinde —
// kalıyor; C# tarafında ikinci bir renk listesi tutulmuyor.
//
// SINIRI: dönüştürücüler tema değiştiğinde kendiliğinden yeniden çalışmaz.
// Bu yüzden tema geçişinde kabuk, açık olan ekranı yeniden oluşturuyor.
public class ThemeBrushConverter : IValueConverter
{
    public static readonly ThemeBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string key || key.Length == 0)
            return Brushes.Transparent;

        // Palette'te olmayan bir ad gelirse, ham renk olabilir mi diye bakılır.
        // Bu geriye dönük bir emniyet: bir yerde hex kaldıysa ekran boş çizilmesin.
        if (Application.Current is { } app &&
            app.TryGetResource(key, app.ActualThemeVariant, out var found) &&
            found is IBrush brush)
        {
            return brush;
        }

        try
        {
            return new SolidColorBrush(Color.Parse(key));
        }
        catch
        {
            return Brushes.Transparent;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

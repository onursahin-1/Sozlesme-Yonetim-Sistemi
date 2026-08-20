using System.Globalization;

namespace Sys.UI;

// Para birimi gösterimi tek yerden yönetilir.
//
// Tutarlar eskiden her ekranda elle " TL" eklenerek yazılıyordu; sözleşmeler artık
// farklı para birimlerinde olabildiği için bu yaklaşım yanlış bilgi gösterme riski
// taşıyordu. Biçimlendirme buraya toplandı.
public static class CurrencyHelper
{
    // Veritabanında ISO kodu saklanır (TRY/EUR/USD); ekranda kısa sembol gösterilir.
    public static readonly string[] Options = { "TRY", "EUR", "USD" };

    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static string Symbol(string? code) => code switch
    {
        "EUR" => "€",
        "USD" => "$",
        _ => "TL"
    };

    // Kullanıcıya seçim listesinde gösterilecek açıklayıcı etiket.
    public static string Label(string? code) => code switch
    {
        "EUR" => "EUR (€)",
        "USD" => "USD ($)",
        _ => "TRY (TL)"
    };

    public static string Format(decimal amount, string? code)
        => amount.ToString("N2", Tr) + " " + Symbol(code);
}

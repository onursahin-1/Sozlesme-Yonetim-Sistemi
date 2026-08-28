using System.Text.Json;

namespace Sys.Services;

// Bildirim metnindeki {0}, {1}... parametrelerinin saklanması.
//
// Parametreler ÇEVRİLMEYEN verilerdir: sözleşme başlığı, kullanıcının yazdığı
// gerekçe, gün sayısı. Bunlar veritabanında olduğu gibi durur; çevrilen tek şey
// onları çevreleyen cümledir.
//
// JSON seçildi çünkü ayraçlı bir dizge (örn. "|" ile birleştirme) gerekçe metninde
// o karakter geçtiğinde bölünürdü — kullanıcının yazdığı serbest metni ayraçla
// saklamak çalışmaz.
//
// Bozuk ya da eski biçimli veri geldiğinde boş dizi döner: bildirim yine gösterilir,
// yalnızca parametreleri eksik kalır. Bir bildirimin okunamaması yüzünden ekranın
// çökmesi kabul edilemez.
public static class NotificationArgs
{
    public static string? Serialize(object?[]? args)
    {
        if (args is null || args.Length == 0) return null;

        try
        {
            return JsonSerializer.Serialize(args.Select(a => a?.ToString() ?? string.Empty).ToArray());
        }
        catch
        {
            return null;
        }
    }

    public static string[] Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}

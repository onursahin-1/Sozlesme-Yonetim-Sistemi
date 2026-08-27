using System;
using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Styling;

namespace Sys.UI;

// Açık/koyu tema seçimi ve bu seçimin hatırlanması.
//
// Tercih veritabanında değil DİSKTE tutuluyor. Sebep: tema bir iş verisi değil,
// o bilgisayardaki görüntü ayarı. Kullanıcı tablosuna alan eklemek migration
// gerektirirdi ve aynı hesapla farklı makinelerden girildiğinde birinin tercihi
// diğerini ezerdi.
//
// Dosya kullanıcının AppData klasöründe; yazılamazsa (salt okunur profil, disk
// dolu) uygulama sessizce varsayılan temayla devam eder — tema tercihinin
// kaydedilememesi uygulamayı engellemez.
public static class ThemeService
{
    private const string FileName = "theme.json";

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SYS", FileName);

    private sealed class ThemeSettings
    {
        public bool IsDark { get; set; }
    }

    public static bool IsDark { get; private set; }

    // Tema değiştiğinde tetiklenir. Kabuk buna abone olup açık ekranı yeniden
    // oluşturuyor: ViewModel'lerin ürettiği renkler (durum rozetleri) bir
    // dönüştürücüden geçiyor ve dönüştürücüler tema değişiminde kendiliğinden
    // yeniden çalışmıyor.
    public static event Action? ThemeChanged;

    // Uygulama açılışında çağrılır: kayıtlı tercih varsa uygulanır.
    public static void Initialize()
    {
        IsDark = Load();
        Apply();
    }

    public static void Toggle() => Set(!IsDark);

    public static void Set(bool isDark)
    {
        if (IsDark == isDark) return;

        IsDark = isDark;
        Apply();
        Save();
        ThemeChanged?.Invoke();
    }

    private static void Apply()
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    private static bool Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return false;
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<ThemeSettings>(json)?.IsDark ?? false;
        }
        catch
        {
            // Bozuk ya da okunamayan dosya: varsayılan açık tema.
            return false;
        }
    }

    private static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (dir is not null) Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new ThemeSettings { IsDark = IsDark }));
        }
        catch
        {
            // Tercih kaydedilemedi; tema bu oturumda yine de geçerli.
        }
    }
}

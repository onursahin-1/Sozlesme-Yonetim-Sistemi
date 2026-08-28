using System;
using System.IO;
using System.Text.Json;

namespace Sys.UI.Localization;

public enum AppLanguage
{
    Tr,
    En
}

// Arayüz dili seçimi ve bu seçimin hatırlanması.
//
// ThemeService ile birebir aynı desen: tercih veritabanında değil DİSKTE tutuluyor.
// Dil bir iş verisi değil, o bilgisayardaki görüntü ayarı. Kullanıcı tablosuna alan
// eklemek migration gerektirirdi ve aynı hesapla farklı makinelerden girildiğinde
// birinin tercihi diğerini ezerdi.
//
// TARİH VE TUTAR BİÇİMİ DİLE BAĞLI DEĞİL. Uygulama her iki dilde de tr-TR biçimini
// kullanmaya devam ediyor (31.12.2026 · 15.678,00 TL). Bu şirket içi bir sistem;
// sözleşme tutarı ve tarihi Türk mevzuatına göre yazılıyor. Dil değişince aynı
// sözleşmenin PDF'i ve Excel çıktısı farklı okunsaydı, kayıtlar birbirini tutmazdı.
public static class LanguageService
{
    private const string FileName = "language.json";

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SYS", FileName);

    private sealed class LanguageSettings
    {
        public string Language { get; set; } = "tr";
    }

    public static AppLanguage Current { get; private set; } = AppLanguage.Tr;

    public static bool IsEnglish => Current == AppLanguage.En;

    public static event Action? LanguageChanged;

    public static void Initialize() => Current = Load();

    public static void Toggle() => Set(IsEnglish ? AppLanguage.Tr : AppLanguage.En);

    public static void Set(AppLanguage language)
    {
        if (Current == language) return;

        Current = language;
        Save();

        // Sözlük önce haber alır, ekranlar sonra: aksi halde ekranlar eski dilin
        // metinlerini okurdu.
        Strings.NotifyLanguageChanged();
        LanguageChanged?.Invoke();
    }

    private static AppLanguage Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return AppLanguage.Tr;
            var json = File.ReadAllText(SettingsPath);
            var code = JsonSerializer.Deserialize<LanguageSettings>(json)?.Language;
            return string.Equals(code, "en", StringComparison.OrdinalIgnoreCase)
                ? AppLanguage.En
                : AppLanguage.Tr;
        }
        catch
        {
            // Bozuk ya da okunamayan dosya: varsayılan Türkçe.
            return AppLanguage.Tr;
        }
    }

    private static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (dir is not null) Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsPath,
                JsonSerializer.Serialize(new LanguageSettings { Language = IsEnglish ? "en" : "tr" }));
        }
        catch
        {
            // Tercih kaydedilemedi; dil bu oturumda yine de geçerli.
        }
    }
}

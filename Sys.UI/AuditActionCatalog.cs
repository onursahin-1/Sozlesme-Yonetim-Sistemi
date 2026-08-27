using System.Collections.Generic;
using System.Linq;

namespace Sys.UI;

// Denetim kaydındaki ham işlem adlarının ("TalepİadeEdildi") okunabilir karşılığı,
// kategorisi ve rengi.
//
// Bu eşleme daha önce AuditLogRowViewModel içinde bir switch'ti ve son turlarda
// eklenen işlemler oraya yansıtılmamıştı: "TalepİadeEdildi", "TalepReddedildi" ve
// "İhlalGiderildi" ekranda ham haliyle görünüyordu. Tek yere alınınca hem filtre
// listesi hem satır etiketi aynı kaynaktan besleniyor, bir daha ayrışamaz.
public static class AuditActionCatalog
{
    // Filtre listesinin sırasını belirler; renkle ilgisi yok.
    public enum Category
    {
        Talep,      // talep yaşam döngüsü
        Sozlesme,   // sözleşme üzerinde yapılan işlemler
        Karar,      // onay / red
        Erisim,     // dosya görüntüleme, indirme, yazdırma
        Hesap       // kullanıcı ve şifre işlemleri
    }

    // Rengi belirleyen şey işlemin SONUCU, ait olduğu modül değil.
    //
    // Önceki sürümde renk kategoriden geliyordu: talep mavi, sözleşme mor, hesap
    // turuncu... Altı renk hem gürültülüydü hem de hiçbir şey söylemiyordu — bir
    // sözleşmenin oluşturulması ile silinmesi aynı moru alıyordu. Artık kayıtların
    // çoğu nötr; göz yalnızca gerçekten dikkat gerektirenlere takılıyor.
    public enum Tone
    {
        Notr,       // olağan iş akışı kaydı
        Olumlu,     // onay, çözüm
        Uyari,      // geri dönüş, yönetici müdahalesi
        Olumsuz     // red, silme, erişim kısıtlama
    }

    public sealed record ActionInfo(string Key, string Label, Category Category, Tone Tone);

    // Sıra, filtre açılır listesindeki sırayı belirler: kategori kategori gruplanmış.
    private static readonly List<ActionInfo> All = new()
    {
        new("TalepOluşturuldu",        "Talep Oluşturuldu",            Category.Talep,    Tone.Notr),
        new("TalepGüncellendi",        "Talep Güncellendi",            Category.Talep,    Tone.Notr),
        new("TalepİadeEdildi",         "Talep İade Edildi",            Category.Talep,    Tone.Uyari),
        new("TalepReddedildi",         "Talep Reddedildi (Kapatıldı)", Category.Talep,    Tone.Olumsuz),

        new("SözleşmeOluşturuldu",     "Sözleşme Oluşturuldu",         Category.Sozlesme, Tone.Notr),
        new("SözleşmeDüzenlendi",      "Sözleşme Düzenlendi",          Category.Sozlesme, Tone.Notr),
        new("FesihTalebiOluşturuldu",  "Fesih Talebi Oluşturuldu",     Category.Sozlesme, Tone.Uyari),
        new("İhlalBildirildi",         "İhlal Bildirildi",             Category.Sozlesme, Tone.Olumsuz),
        new("İhlalGiderildi",          "İhlal Giderildi",              Category.Sozlesme, Tone.Olumlu),

        new("Onaylandı",               "Onaylandı",                    Category.Karar,    Tone.Olumlu),
        new("Reddedildi",              "Reddedildi",                   Category.Karar,    Tone.Olumsuz),

        new("EkGörüntülendi",          "Ek Görüntülendi",              Category.Erisim,   Tone.Notr),
        new("Ekİndirildi",             "Ek İndirildi",                 Category.Erisim,   Tone.Notr),
        new("EkSilindi",               "Ek Silindi",                   Category.Erisim,   Tone.Olumsuz),
        new("SözleşmeYazdırıldı",      "Sözleşme Yazdırıldı",          Category.Erisim,   Tone.Notr),
        // Dışa aktarma toplu veri çıkışıdır; tek bir sözleşmenin yazdırılmasından
        // daha dikkat çekici olduğu için uyarı tonunda.
        new("ListeDışaAktarıldı",      "Liste Excel'e Aktarıldı",      Category.Erisim,   Tone.Uyari),

        new("KullanıcıOluşturuldu",    "Kullanıcı Oluşturuldu",        Category.Hesap,    Tone.Notr),
        new("ŞifreSıfırlandı",         "Şifre Sıfırlandı (Yönetici)",  Category.Hesap,    Tone.Uyari),
        new("ŞifreDeğiştirildi",       "Şifre Değiştirildi",           Category.Hesap,    Tone.Notr),
        new("HesapDevreDışıBırakıldı", "Hesap Devre Dışı Bırakıldı",   Category.Hesap,    Tone.Olumsuz),
        new("HesapEtkinleştirildi",    "Hesap Etkinleştirildi",        Category.Hesap,    Tone.Olumlu),
        new("HesapKilitlendi",         "Hesap Kilitlendi",             Category.Hesap,    Tone.Olumsuz),
    };

    private static readonly Dictionary<string, ActionInfo> ByKey =
        All.ToDictionary(a => a.Key);

    public static IReadOnlyList<ActionInfo> Actions => All;

    // Kataloğa girmemiş bir işlem adı gelirse ham hâliyle gösterilir; ekran
    // boş kalmaz, eksik eşleme de fark edilir.
    public static string Label(string action)
        => ByKey.TryGetValue(action, out var info) ? info.Label : action;

    public static Category CategoryOf(string action)
        => ByKey.TryGetValue(action, out var info) ? info.Category : Category.Sozlesme;

    public static Tone ToneOf(string action)
        => ByKey.TryGetValue(action, out var info) ? info.Tone : Tone.Notr;

    // Rozet yazı rengi. Nötr kayıtlar sakin bir gri-mavi; renk yalnızca sonucu olan
    // işlemlerde devreye giriyor.
    public static string ColorHexFor(string action) => ToneOf(action) switch
    {
        Tone.Olumlu => "SuccessBase",
        Tone.Uyari => "WarningBase",
        Tone.Olumsuz => "DangerBase",
        _ => "TextLabel"
    };

    public static string BgHexFor(string action) => ToneOf(action) switch
    {
        Tone.Olumlu => "SuccessSoftBgAlt",
        Tone.Uyari => "WarningSoftBgAlt",
        Tone.Olumsuz => "DangerSoftBgFaint3",
        _ => "SurfaceSubtle"
    };

    // Satırın solundaki ince şerit. Nötr kayıtlarda neredeyse görünmez kalıyor;
    // liste sakin duruyor, dikkat gerektiren satırlar kendiliğinden öne çıkıyor.
    public static string StripHexFor(string action) => ToneOf(action) switch
    {
        Tone.Olumlu => "SuccessBase",
        Tone.Uyari => "WarningBright",
        Tone.Olumsuz => "DangerMuted",
        _ => "BorderFaint"
    };
}

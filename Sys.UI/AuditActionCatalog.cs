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

    // Label değil LabelKey: gösterilecek metin çeviri sözlüğünden çözülüyor.
    // Key ise VERİTABANINDA yazan değer — o hiçbir zaman çevrilmez.
    public sealed record ActionInfo(string Key, string LabelKey, Category Category, Tone Tone)
    {
        public string Label => Localization.Strings.T(LabelKey);
    }

    // Sıra, filtre açılır listesindeki sırayı belirler: kategori kategori gruplanmış.
    private static readonly List<ActionInfo> All = new()
    {
        new("TalepOluşturuldu", "Act.TalepOluşturuldu",            Category.Talep,    Tone.Notr),
        new("TalepGüncellendi", "Act.TalepGüncellendi",            Category.Talep,    Tone.Notr),
        new("TalepİadeEdildi", "Act.TalepİadeEdildi",            Category.Talep,    Tone.Uyari),
        new("TalepGeriÇekildi", "Act.TalepGeriÇekildi", Category.Talep,    Tone.Uyari),
        new("TalepReddedildi", "Act.TalepReddedildi", Category.Talep,    Tone.Olumsuz),

        new("SözleşmeOluşturuldu", "Act.SözleşmeOluşturuldu",         Category.Sozlesme, Tone.Notr),
        new("SözleşmeDüzenlendi", "Act.SözleşmeDüzenlendi",          Category.Sozlesme, Tone.Notr),
        new("FesihTalebiOluşturuldu", "Act.FesihTalebiOluşturuldu",     Category.Sozlesme, Tone.Uyari),
        new("İhlalBildirildi", "Act.İhlalBildirildi",             Category.Sozlesme, Tone.Olumsuz),
        new("İhlalGiderildi", "Act.İhlalGiderildi",              Category.Sozlesme, Tone.Olumlu),

        new("Onaylandı", "Act.Onaylandı",                    Category.Karar,    Tone.Olumlu),
        new("Reddedildi", "Act.Reddedildi",                   Category.Karar,    Tone.Olumsuz),

        new("EkGörüntülendi", "Act.EkGörüntülendi",              Category.Erisim,   Tone.Notr),
        new("Ekİndirildi", "Act.Ekİndirildi",                 Category.Erisim,   Tone.Notr),
        new("EkSilindi", "Act.EkSilindi",                   Category.Erisim,   Tone.Olumsuz),
        new("SözleşmeYazdırıldı", "Act.SözleşmeYazdırıldı",          Category.Erisim,   Tone.Notr),
        // Dışa aktarma toplu veri çıkışıdır; tek bir sözleşmenin yazdırılmasından
        // daha dikkat çekici olduğu için uyarı tonunda.
        new("ListeDışaAktarıldı", "Act.ListeDışaAktarıldı",      Category.Erisim,   Tone.Uyari),

        new("KullanıcıOluşturuldu", "Act.KullanıcıOluşturuldu",        Category.Hesap,    Tone.Notr),
        new("ŞifreSıfırlandı", "Act.ŞifreSıfırlandı",  Category.Hesap,    Tone.Uyari),
        new("ŞifreDeğiştirildi", "Act.ŞifreDeğiştirildi",           Category.Hesap,    Tone.Notr),
        new("HesapDevreDışıBırakıldı", "Act.HesapDevreDışıBırakıldı",   Category.Hesap,    Tone.Olumsuz),
        new("HesapEtkinleştirildi", "Act.HesapEtkinleştirildi",        Category.Hesap,    Tone.Olumlu),
        new("HesapKilitlendi", "Act.HesapKilitlendi",             Category.Hesap,    Tone.Olumsuz),
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

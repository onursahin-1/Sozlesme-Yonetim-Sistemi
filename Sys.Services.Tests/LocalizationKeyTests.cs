using System.Text.RegularExpressions;
using Sys.Services;

namespace Sys.Services.Tests;

// Çeviri anahtarlarının tutarlılığı.
//
// NEDEN KAYNAK DOSYALARI TARANIYOR?
// Sözlükte olmayan bir anahtar derlenmeyi engellemez: `Strings.T("Nav.Arsiv")`
// yazım hatasıyla da derlenir, ekranda "[Nav.Arsiv]" olarak görünür. Bu tasarım
// bilinçli (boşluk bırakmaktansa göze batsın), ama fark edilmesi o ekrana biri
// bakana kadar gecikir.
//
// Tarama bir kez elle yapıldı ve temiz çıktı; buraya taşınmasının sebebi TEKRAR
// EDİLEBİLİR olması. Yeni bir ekran eklenip anahtarı sözlüğe konmazsa, testler
// bunu ekranda köşeli parantez görülmeden önce söyler.
//
// Test, Sys.UI'ye BAŞVURMUYOR — Avalonia bağımlılığını test projesine sokmamak
// için dosyalar metin olarak okunuyor. Ödediğimiz bedel: yol bulma mantığı.
public class LocalizationKeyTests
{
    // Sözlükteki tanımlar: ["Nav.Dashboard"] = ("...", "...")
    private static readonly Regex Definition = new(@"\[""([A-Za-z0-9_.çğıöşüÇĞİÖŞÜ]+)""\]\s*=", RegexOptions.Compiled);

    // Anahtarın KENDİ tırnaklarına doğrudan bakılıyor: "Bolum.Ad".
    //
    // Önce "her dizgeyi yakala, sonra anahtar mı diye bak" denendi; enterpolasyonlu
    // metinlerde ($"... {Strings.T("Out.Total")} ...") tırnaklar yanlış eşleşiyor ve
    // içteki anahtar hiç görünmüyordu. Testin ilk çalıştırmasında dört anahtar
    // "kullanılmıyor" göründü — kullanılıyorlardı, desen kördü.
    private static readonly Regex QuotedKey =
        new(@"""([A-Z][A-Za-z]+\.[A-Za-z0-9çğıöşüÇĞİÖŞÜ]+)""", RegexOptions.Compiled);
    private static readonly Regex XamlBinding = new(@"\{Binding \[([^\]]+)\]", RegexOptions.Compiled);

    [Fact]
    public void EveryUsedKey_IsDefinedInDictionary()
    {
        var (defined, used) = Scan();

        var missing = used.Except(defined).OrderBy(k => k).ToList();

        Assert.True(missing.Count == 0,
            "Sözlükte olmayan anahtarlar (ekranda köşeli parantezle görünür):\n  " +
            string.Join("\n  ", missing));
    }

    // Kullanılmayan anahtar zararsız görünür ama sözlüğü şişirir ve daha kötüsü,
    // aynı metin için ikinci bir anahtar açıldığında ikisi zamanla ayrışır —
    // bu projede tam olarak bu oldu (Vio.Reported / Vio.Saved gibi).
    [Fact]
    public void EveryDefinedKey_IsUsedSomewhere()
    {
        var (defined, used) = Scan();

        var unused = defined.Except(used).OrderBy(k => k).ToList();

        Assert.True(unused.Count == 0,
            "Hiçbir yerde kullanılmayan anahtarlar:\n  " + string.Join("\n  ", unused));
    }

    private static (HashSet<string> Defined, HashSet<string> Used) Scan()
    {
        var root = FindRepositoryRoot();
        var stringsFile = Path.Combine(root, "Sys.UI", "Localization", "Strings.cs");

        var defined = Definition.Matches(File.ReadAllText(stringsFile))
            .Select(m => m.Groups[1].Value)
            .ToHashSet();

        // Sözlükteki bölüm önekleri ("Nav", "Err", "Ntf"…). Kaynakta geçen
        // anahtar biçimli her dizgeyi anahtar saymak yanlış olurdu: XAML stil
        // seçicileri de aynı biçimde ("TextBlock.cardTitle"). Önek süzgeci bu
        // ikisini ayırıyor.
        var prefixes = defined
            .Select(k => k[..k.IndexOf('.')])
            .ToHashSet();

        var used = new HashSet<string>();

        foreach (var file in SourceFiles(root))
        {
            var text = File.ReadAllText(file);

            foreach (Match m in QuotedKey.Matches(text))
            {
                var value = m.Groups[1].Value;

                // Önek süzgeci: XAML stil seçicileri de anahtar biçiminde
                // ("TextBlock.cardTitle"). Sözlükte böyle bir bölüm yok.
                if (!prefixes.Contains(value[..value.IndexOf('.')])) continue;
                used.Add(value);
            }

            foreach (Match m in XamlBinding.Matches(text))
                used.Add(m.Groups[1].Value);
        }

        // Hata anahtarları koddan TÜRETİLİYOR ("Err." + AppError değeri), kaynakta
        // dizge olarak geçmiyorlar. Enum'dan üretilmezlerse hepsi "kullanılmıyor"
        // görünürdü.
        foreach (var name in Enum.GetNames<AppError>())
            used.Add("Err." + name);

        return (defined, used);
    }

    private static IEnumerable<string> SourceFiles(string root)
    {
        foreach (var dir in new[] { "Sys.UI", "Sys.Services" })
        {
            var path = Path.Combine(root, dir);
            if (!Directory.Exists(path)) continue;

            foreach (var file in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                    file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;

                var ext = Path.GetExtension(file);
                if (ext is not (".cs" or ".axaml")) continue;

                // Sözlüğün kendisi hariç: oradaki her anahtar tanım, kullanım değil.
                if (file.EndsWith(Path.Combine("Localization", "Strings.cs"))) continue;

                yield return file;
            }
        }
    }

    // Test çalışırken bin/Debug altındayız; depo kökünü yukarı doğru arayarak
    // buluyoruz. Bulunamazsa sessizce geçmek yerine açıkça hata veriyoruz —
    // hiç çalışmayan bir test, olmayan testten daha kötüdür.
    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Sys.UI", "Localization", "Strings.cs")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Depo kökü bulunamadı: Sys.UI/Localization/Strings.cs aranırken " +
            AppContext.BaseDirectory + " konumundan yukarı çıkıldı.");
    }
}

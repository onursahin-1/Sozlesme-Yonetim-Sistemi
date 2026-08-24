using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Sys.Domain;

namespace Sys.UI.Printing;

// Belgeyi yazdırma ve dosya açma yardımcıları.
//
// Avalonia'nın yerleşik yazdırma API'si yok. İlk çözüm PDF üretip kabuğun "print"
// fiilini çağırmaktı; ancak Windows'ta varsayılan PDF uygulaması genellikle Edge
// oluyor ve Edge ".pdf" için bu fiili KAYDETMİYOR. Sonuç: yazdırma hiç başlamıyor,
// dosya yalnızca bir sekmede açılıyordu.
//
// Şimdiki yol: künye HTML olarak üretiliyor ve varsayılan tarayıcıda açılıyor;
// sayfa yüklenir yüklenmez window.print() çalışıp tarayıcının yazdırma penceresini
// (yazıcı seçimi, önizleme, sayfa düzeni) açıyor.
public static class DocumentPrinter
{
    public static void PrintContract(Contract contract, string fileNameBase)
    {
        var html = ContractPrintDocument.BuildHtml(contract);

        // Geçici klasöre yazılıyor; kullanıcıya dosya seçtirmeye gerek yok.
        // Aynı sözleşme tekrar yazdırıldığında dosya üzerine yazılır, çöp birikmez.
        var path = Path.Combine(Path.GetTempPath(), $"SYS_{Sanitize(fileNameBase)}.html");
        File.WriteAllText(path, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        Open(path);
    }

    public static void Open(string path)
    {
        var psi = new ProcessStartInfo(path) { UseShellExecute = true };
        Process.Start(psi);
    }

    private static string Sanitize(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(name) ? "sozlesme" : name;
    }
}

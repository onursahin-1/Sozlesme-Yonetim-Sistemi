using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sys.Infrastructure;

public static class AttachmentFileHelper
{
    private static readonly string[] AllowedExtensions = { ".pdf", ".docx", ".xlsx", ".jpg", ".png" };
    private const long MaxSizeBytes = 10 * 1024 * 1024; // 10 MB

    public static string SaveFile(string sourceFilePath, string basePath, int contractId)
    {
        var ext = Path.GetExtension(sourceFilePath).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new InvalidOperationException($"Desteklenmeyen dosya türü: {ext}. İzin verilenler: PDF, DOCX, XLSX, JPG, PNG.");

        var fileInfo = new FileInfo(sourceFilePath);
        if (fileInfo.Length > MaxSizeBytes)
            throw new InvalidOperationException("Dosya boyutu 10 MB'ı aşamaz.");

        if (!MatchesExpectedFileSignature(sourceFilePath, ext))
            throw new InvalidOperationException("Dosya içeriği uzantısıyla eşleşmiyor. Dosya bozuk olabilir veya uzantısı değiştirilmiş olabilir.");

        var targetDir = Path.GetFullPath(Path.Combine(basePath, contractId.ToString()));
        Directory.CreateDirectory(targetDir);

        var fileName = SanitizeFileName(Path.GetFileName(sourceFilePath), ext);
        var targetPath = Path.Combine(targetDir, fileName);

        // Aynı sözleşmeye aynı isimde ikinci bir dosya yüklenirse öncekini
        // sessizce ezmek yerine, Windows'un "dosya (1).pdf" mantığına benzer
        // şekilde benzersiz bir isim üretilir.
        if (File.Exists(targetPath))
        {
            var nameOnly = Path.GetFileNameWithoutExtension(fileName);
            var fileExt = Path.GetExtension(fileName);
            var counter = 1;
            do
            {
                targetPath = Path.Combine(targetDir, $"{nameOnly} ({counter}){fileExt}");
                counter++;
            } while (File.Exists(targetPath));
        }

        // Son savunma hattı: temizleyicinin kaçırdığı bir durum kalırsa dosya
        // sözleşme klasörünün DIŞINA yazılmamalı. Temizleyiciye güvenip bu kontrolü
        // atlamak, ileride temizleyici değiştiğinde sessiz bir açık bırakırdı.
        var resolved = Path.GetFullPath(targetPath);
        var dirPrefix = targetDir.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(dirPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Dosya adı geçersiz.");

        File.Copy(sourceFilePath, resolved, overwrite: false);
        return resolved;
    }

    // Diskte kullanılacak dosya adını güvenli hâle getirir.
    //
    // Kullanıcıya gösterilen ad (Attachment.FileName) değişmez; burada üretilen
    // yalnızca DOSYA SİSTEMİNDEKİ addır. İkisini ayırmak, ekranda okunaklı adı
    // korurken diske yazarken güvenli davranmayı sağlıyor.
    //
    // Pratikte ad Windows dosya seçicisinden geliyor ve çoğu durum zaten
    // oluşmuyor. Yine de Path.Combine'a giden ham bir kullanıcı girdisi;
    // "nasılsa dosya seçiciden geliyor" varsayımı, girdi bir gün başka bir
    // yerden gelmeye başladığında sessizce çöker.
    public static string SanitizeFileName(string? rawName, string extension)
    {
        var name = Path.GetFileNameWithoutExtension(rawName ?? string.Empty);

        // Dosya sisteminde geçersiz karakterler alt çizgiye çevrilir.
        //
        // '/' ve '\' AYRICA elle ekleniyor: Path.GetInvalidFileNameChars() platforma
        // göre değişiyor ve Linux'ta ters bölü geçerli bir karakter sayılıyor. Bu
        // kural bir güvenlik garantisi; çalıştığı işletim sistemine göre değişmemeli.
        var invalid = new HashSet<char>(Path.GetInvalidFileNameChars()) { '/', '\\' };
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());

        // Windows sondaki nokta ve boşlukları sessizce kırpıyor; "dosya." adı
        // "dosya" olarak yazılıyor ve File.Exists kontrolü şaşıyordu.
        cleaned = cleaned.Trim().TrimEnd('.', ' ');

        // Yalnızca noktalardan oluşan adlar ("." / "..") burada boşa düşer.
        if (cleaned.Length == 0) cleaned = "belge";

        // Windows'ta ayrılmış aygıt adları: CON.pdf gibi bir dosya oluşturulamaz
        // ve hata mesajı kullanıcıya hiçbir şey anlatmaz.
        if (ReservedNames.Contains(cleaned)) cleaned = "_" + cleaned;

        // Uzun adlar yol sınırını aşıp anlaşılmaz bir hata veriyordu. Uzantı
        // korunuyor; kesilen kısım yalnızca addan gidiyor. Kesme işlemi sondaki
        // noktayı/boşluğu geri getirebildiği için yeniden kırpılıyor.
        if (cleaned.Length > MaxNameLength)
        {
            cleaned = cleaned[..MaxNameLength].TrimEnd('.', ' ');
            if (cleaned.Length == 0) cleaned = "belge";
        }

        return cleaned + extension;
    }

    private const int MaxNameLength = 100;

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    // Dosyanın ilk birkaç byte'ına ("dosya imzası" / magic bytes) bakarak
    // içeriğin gerçekten iddia edilen türde olup olmadığını doğrular.
    // Sadece uzantıya bakmak yeterli değil çünkü bir dosya kolayca yeniden
    // adlandırılabilir (örn. zararlı_dosya.exe -> sozlesme.pdf).
    private static bool MatchesExpectedFileSignature(string filePath, string extension)
    {
        var header = new byte[8];
        int bytesRead;
        using (var stream = File.OpenRead(filePath))
        {
            bytesRead = stream.Read(header, 0, header.Length);
        }
        if (bytesRead < header.Length)
            Array.Resize(ref header, bytesRead);

        return extension switch
        {
            ".pdf" => StartsWith(header, 0x25, 0x50, 0x44, 0x46), // %PDF
            ".jpg" => StartsWith(header, 0xFF, 0xD8, 0xFF),
            ".png" => StartsWith(header, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),
            // .docx ve .xlsx teknik olarak birer ZIP arşividir (OOXML formatı);
            // ikisini birbirinden ayırmak için arşivin içini açmak gerekir, o yüzden
            // burada "gerçekten bir ZIP mi" kontrolü yapılıyor.
            ".docx" or ".xlsx" => StartsWith(header, 0x50, 0x4B),
            _ => true
        };
    }

    private static bool StartsWith(byte[] header, params byte[] expected)
    {
        if (header.Length < expected.Length) return false;
        for (int i = 0; i < expected.Length; i++)
        {
            if (header[i] != expected[i]) return false;
        }
        return true;
    }
}
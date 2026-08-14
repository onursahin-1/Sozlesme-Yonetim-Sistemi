using System;
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

        var targetDir = Path.Combine(basePath, contractId.ToString());
        Directory.CreateDirectory(targetDir);

        var fileName = Path.GetFileName(sourceFilePath);
        var targetPath = Path.Combine(targetDir, fileName);
        File.Copy(sourceFilePath, targetPath, overwrite: true);
        return targetPath;
    }

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
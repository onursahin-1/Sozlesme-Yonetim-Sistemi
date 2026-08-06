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

        var targetDir = Path.Combine(basePath, contractId.ToString());
        Directory.CreateDirectory(targetDir);

        var fileName = Path.GetFileName(sourceFilePath);
        var targetPath = Path.Combine(targetDir, fileName);
        File.Copy(sourceFilePath, targetPath, overwrite: true);
        return targetPath;
    }
}
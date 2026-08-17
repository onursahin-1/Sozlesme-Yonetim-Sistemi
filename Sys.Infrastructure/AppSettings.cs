using System;
using System.IO;
using System.Text.Json;

namespace Sys.Infrastructure;

public class AppSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string AttachmentsPath { get; set; } = string.Empty;
}

public static class AppSettingsLoader
{
    public static AppSettings Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.Local.json");

        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                "appsettings.Local.json bulunamadı. " +
                "appsettings.Local.json.example dosyasını 'appsettings.Local.json' olarak kopyalayıp " +
                "kendi bağlantı bilgilerinizi girin.\nBeklenen konum: " + path);
        }

        AppSettings? settings;
        try
        {
            var json = File.ReadAllText(path);
            settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "appsettings.Local.json okunamadı, dosya geçerli bir JSON formatında değil: " + ex.Message, ex);
        }

        if (settings is null || string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            throw new InvalidOperationException(
                "appsettings.Local.json içinde 'ConnectionString' alanı boş veya eksik.");
        }

        return settings;
    }
}
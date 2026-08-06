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
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
    }
}
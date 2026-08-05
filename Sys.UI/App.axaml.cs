using System;
using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Sys.Infrastructure;
using Sys.UI.ViewModels;
using Sys.UI.Views;

namespace Sys.UI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                var connectionString = LoadConnectionString();
                System.Threading.Tasks.Task.Run(async () =>
                {
                    using var db = DbConnectionFactory.CreateContext(connectionString);
                    await DbSeeder.SeedAsync(db);
                }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                var logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "sys-startup-error.txt");
                File.WriteAllText(logPath, ex.ToString());
            }

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static string LoadConnectionString()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.Local.json");
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("ConnectionString").GetString()!;
    }
}
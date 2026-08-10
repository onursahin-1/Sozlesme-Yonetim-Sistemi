using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Sys.Infrastructure;
using Sys.Services;
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
            var settings = AppSettingsLoader.Load();
            var db = DbConnectionFactory.CreateContext(settings.ConnectionString);

            var userRepository = new UserRepository(db);
            var authService = new AuthService(userRepository);

            var contractRepository = new ContractRepository(db);
            var attachmentRepository = new AttachmentRepository(db);
            var contractService = new ContractService(contractRepository, attachmentRepository);

            try
            {
                Task.Run(async () =>
                {
                    await DbSeeder.SeedAsync(db);
                    await contractService.ReconcileContractStatusesAsync();
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
                DataContext = new MainViewModel(authService, contractService, settings.AttachmentsPath),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
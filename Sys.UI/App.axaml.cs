using System;
using System.IO;
using System.Threading;
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
    private Timer? _reconcileTimer;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = AppSettingsLoader.Load();

            var userRepository = new UserRepository(settings.ConnectionString);
            var authService = new AuthService(userRepository);

            var contractRepository = new ContractRepository(settings.ConnectionString);
            var attachmentRepository = new AttachmentRepository(settings.ConnectionString);
            var contractService = new ContractService(contractRepository, attachmentRepository);

            try
            {
                Task.Run(async () =>
                {
                    using var seedDb = DbConnectionFactory.CreateContext(settings.ConnectionString);
                    await DbSeeder.SeedAsync(seedDb);
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

            // Uygulama açık kaldığı sürece, her saat başı sözleşme durumlarını
            // (Aktif/Uyarı/Tamamlandı) otomatik olarak yeniden değerlendirir.
            // Böylece gün içinde süresi dolan bir sözleşme, uygulama yeniden
            // açılana kadar beklemeden güncellenir.
            _reconcileTimer = new Timer(async _ =>
            {
                try
                {
                    await contractService.ReconcileContractStatusesAsync();
                }
                catch (Exception ex)
                {
                    var logPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        "sys-reconcile-error.txt");
                    File.WriteAllText(logPath, ex.ToString());
                }
            }, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(authService, contractService, settings.AttachmentsPath),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
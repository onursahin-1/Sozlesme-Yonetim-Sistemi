using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
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
            AppSettings settings;
            try
            {
                settings = AppSettingsLoader.Load();
            }
            catch (Exception ex)
            {
                // appsettings.Local.json eksik/bozuksa uygulama ham bir çökme yerine
                // anlaşılır bir hata bırakıp düzgünce kapanır.
                var logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "sys-startup-error.txt");
                File.WriteAllText(logPath, "SYS başlatılamadı: ayarlar yüklenemedi.\n\n" + ex);
                desktop.Shutdown();
                return;
            }

            var userRepository = new UserRepository(settings.ConnectionString);
            var authService = new AuthService(userRepository);
            var userManagementService = new UserManagementService(userRepository);

            var contractRepository = new ContractRepository(settings.ConnectionString);
            var attachmentRepository = new AttachmentRepository(settings.ConnectionString);
            var notificationRepository = new NotificationRepository(settings.ConnectionString);
            var contractService = new ContractService(contractRepository, attachmentRepository, notificationRepository, userRepository);
            var notificationService = new NotificationService(notificationRepository, contractRepository, userRepository);

            try
            {
                Task.Run(async () =>
                {
                    using var seedDb = DbConnectionFactory.CreateContext(settings.ConnectionString);
                    // Uygulama her açıldığında veritabanı şemasını en güncel migration'a
                    // taşır. Bu olmadan boş/eski bir veritabanında seed işlemi ve
                    // sonrasındaki tüm sorgular başarısız olabilir.
                    await seedDb.Database.MigrateAsync();

                    // Bilinen (sabit) şifreli test kullanıcıları yalnızca appsettings.Local.json'da
                    // "EnableDevSeed": true açıkça belirtilmişse oluşturulur. Bu satır olmadan
                    // (örn. bir sunucu/paylaşımlı ortam kurulumunda) seed hiç çalışmaz.
                    if (settings.EnableDevSeed)
                        await DbSeeder.SeedAsync(seedDb);

                    await contractService.ReconcileContractStatusesAsync();

                    // Durumlar güncellendikten sonra "yaklaşan bitiş" bildirimleri üretilir;
                    // böylece kullanıcı giriş yaptığında bildirimler hazır olur.
                    await notificationService.GenerateUpcomingEndingNotificationsAsync();
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
                    await notificationService.GenerateUpcomingEndingNotificationsAsync();
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
                DataContext = new MainViewModel(authService, contractService, userManagementService, notificationService, settings.AttachmentsPath),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
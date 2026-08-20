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
            var passwordResetRequestRepository = new PasswordResetRequestRepository(settings.ConnectionString);
            var notificationRepository = new NotificationRepository(settings.ConnectionString);
            var auditLogRepository = new AuditLogRepository(settings.ConnectionString);

            // AuthService bildirim deposunu da alır: şifre sıfırlama talebi geldiğinde
            // Admin'lere zil bildirimi gönderilir. Denetim kaydı deposu ise hesap
            // kilitlenmesi ve şifre değişikliği gibi güvenlik olayları için.
            var authService = new AuthService(userRepository, passwordResetRequestRepository, notificationRepository, auditLogRepository);
            var userManagementService = new UserManagementService(userRepository, passwordResetRequestRepository, auditLogRepository);

            var contractRepository = new ContractRepository(settings.ConnectionString);
            var attachmentRepository = new AttachmentRepository(settings.ConnectionString);
            var scheduledJobRepository = new ScheduledJobRepository(settings.ConnectionString);
            var contractService = new ContractService(contractRepository, attachmentRepository, notificationRepository, userRepository);
            var notificationService = new NotificationService(notificationRepository, contractRepository, userRepository);
            var maintenanceService = new MaintenanceService(scheduledJobRepository, contractService, notificationService);

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

                    // Bakım işi (durum güncelleme + yaklaşan bitiş bildirimleri) artık
                    // MaintenanceService üzerinden çalışıyor. Son bir saat içinde başka bir
                    // istemci çalıştırdıysa burada sessizce atlanır — veriler zaten günceldir.
                    await maintenanceService.RunHourlyMaintenanceAsync();
                }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                var logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "sys-startup-error.txt");
                File.WriteAllText(logPath, ex.ToString());
            }

            // Uygulama açık kaldığı sürece, her saat başı bakım işi denenir: sözleşme
            // durumları (Aktif/Uyarı/Tamamlandı) yeniden değerlendirilir ve yaklaşan
            // bitiş bildirimleri üretilir. Böylece gün içinde süresi dolan bir sözleşme,
            // uygulama yeniden açılana kadar beklemeden güncellenir.
            //
            // Zamanlayıcı her istemcide çalışsa da işi yalnızca kilidi alan bir istemci
            // yürütür; diğerleri sessizce atlar. Kontrol aralığı 15 dakikaya çekildi:
            // iş zaten saatte bir kez çalışıyor, sık deneme sadece "kilidi alan istemci
            // kapanırsa bir sonrakinin devralması"nı hızlandırıyor.
            _reconcileTimer = new Timer(async _ =>
            {
                try
                {
                    await maintenanceService.RunHourlyMaintenanceAsync();
                }
                catch (Exception ex)
                {
                    var logPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        "sys-reconcile-error.txt");
                    File.WriteAllText(logPath, ex.ToString());
                }
            }, null, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15));

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(authService, contractService, userManagementService, notificationService, settings.AttachmentsPath),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
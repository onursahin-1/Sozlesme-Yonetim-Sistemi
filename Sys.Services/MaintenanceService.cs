using Sys.Domain;

namespace Sys.Services;

// Saatlik bakım işi: sözleşme durumlarını yeniden değerlendirir ve yaklaşan bitiş
// bildirimlerini üretir.
//
// Bu iş eskiden doğrudan App.axaml.cs içindeki zamanlayıcıdan çağrılıyordu; her
// kullanıcının bilgisayarında ayrı ayrı çalışıyordu. Artık çalıştırmadan önce
// veritabanı üzerinden kilit alınıyor: aynı saat diliminde işi yalnızca bir istemci
// üstleniyor, diğerleri sessizce atlıyor.
//
// İş mantığı bilerek bu sınıfta toplandı; ileride sunucuda çalışan bir konsol
// uygulaması/zamanlanmış görev eklenirse, aynı metodu çağırması yeterli olur.
public class MaintenanceService
{
    public const string HourlyJobName = "HourlyMaintenance";

    private readonly IScheduledJobRepository _jobs;
    private readonly ContractService _contracts;
    private readonly NotificationService _notifications;

    // İşin ne sıklıkla çalışacağı. Bir istemci kilidi alamazsa, son koşunun üzerinden
    // bu süre geçene kadar kimse tekrar çalıştıramaz.
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    // Kilit süresi. İşi üstlenen istemci çökerse kilit bu süre sonunda kendiliğinden
    // serbest kalır; aksi halde iş kalıcı olarak bloke olurdu.
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(10);

    public MaintenanceService(
        IScheduledJobRepository jobs,
        ContractService contracts,
        NotificationService notifications)
    {
        _jobs = jobs;
        _contracts = contracts;
        _notifications = notifications;
    }

    // Çalıştırıldıysa true, kilit alınamadığı (başkası yaptı / vakti gelmedi) için
    // atlandıysa false döner.
    public async Task<bool> RunHourlyMaintenanceAsync()
    {
        await _jobs.EnsureJobExistsAsync(HourlyJobName);

        var owner = Environment.MachineName;
        if (!await _jobs.TryAcquireAsync(HourlyJobName, Interval, Lease, owner))
            return false;

        string result;
        try
        {
            var updated = await _contracts.ReconcileContractStatusesAsync();
            var created = await _notifications.GenerateUpcomingEndingNotificationsAsync();
            result = $"{updated} sözleşme durumu güncellendi, {created} bildirim üretildi.";
        }
        catch (Exception ex)
        {
            // Hata olsa bile kilit bırakılmalı; aksi halde iş, kilit süresi dolana kadar
            // (10 dk) askıda kalırdı. Hata özeti tabloya yazılır ki sonradan görülebilsin.
            result = "Hata: " + ex.Message;
            await _jobs.ReleaseAsync(HourlyJobName, result);
            throw;
        }

        await _jobs.ReleaseAsync(HourlyJobName, result);
        return true;
    }
}

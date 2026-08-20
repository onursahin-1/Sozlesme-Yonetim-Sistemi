namespace Sys.Services;

public interface IScheduledJobRepository
{
    // İş satırı yoksa oluşturur. Aynı anda iki istemci denerse unique index devreye girer
    // ve ikincisi sessizce geçer.
    Task EnsureJobExistsAsync(string jobName);

    // İşi üstlenmeye çalışır. Yalnızca (a) son koşunun üzerinden 'interval' kadar süre
    // geçmişse ve (b) başka bir istemcinin geçerli kilidi yoksa true döner.
    // Kontrol ve kilitleme TEK bir atomik UPDATE ile yapılır; iki istemci aynı anda
    // denerse yalnızca biri true alır.
    Task<bool> TryAcquireAsync(string jobName, TimeSpan interval, TimeSpan lease, string owner);

    // İş bittiğinde kilidi bırakır ve son koşu zamanını işaretler.
    Task ReleaseAsync(string jobName, string? result);
}

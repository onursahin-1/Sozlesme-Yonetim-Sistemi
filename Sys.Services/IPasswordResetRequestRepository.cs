using Sys.Domain;

namespace Sys.Services;

public interface IPasswordResetRequestRepository
{
    Task AddAsync(PasswordResetRequest request);

    // Admin ekranında gösterilecek, henüz işlenmemiş talepler (en yeniden eskiye).
    Task<List<PasswordResetRequest>> GetPendingAsync();

    // Belirli bir kullanıcının bekleyen taleplerini işlenmiş olarak işaretler.
    // Admin şifreyi sıfırladığında otomatik olarak çağrılır.
    Task MarkHandledForUserAsync(int userId, int handledByUserId);

    // Tek bir talebi işlenmiş olarak kapatır (örn. kullanıcı adı hatalı girilmiş,
    // yapılacak bir şey yok).
    Task MarkHandledAsync(int requestId, int handledByUserId);

    // Aynı kullanıcının kısa süre içinde tekrar tekrar talep oluşturmasını engellemek
    // için, son talebin zamanını döner. Kayıt yoksa null.
    Task<DateTime?> GetLastRequestAtAsync(string username);
}

using Sys.Domain;

namespace Sys.Services;

public class AuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordResetRequestRepository? _resetRequests;
    private readonly INotificationRepository? _notifications;
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    // Aynı kullanıcı adı için bu süre içinde ikinci bir talep oluşturulmaz; aksi halde
    // giriş ekranından Admin'in listesi kolayca doldurulabilirdi.
    private static readonly TimeSpan ResetRequestCooldown = TimeSpan.FromMinutes(10);

    public AuthService(
        IUserRepository users,
        IPasswordResetRequestRepository? resetRequests = null,
        INotificationRepository? notifications = null)
    {
        _users = users;
        _resetRequests = resetRequests;
        _notifications = notifications;
    }

    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        var user = await _users.GetByUsernameAsync(username);
        if (user is null)
            return AuthResult.Fail("Kullanıcı adı veya şifre hatalı.");

        if (user.IsDisabled)
            return AuthResult.Fail("Bu hesap devre dışı bırakılmış. Yöneticinizle iletişime geçin.");

        if (user.LockedUntil is not null && user.LockedUntil > DateTime.UtcNow)
            return AuthResult.Fail($"Hesap kilitli. {user.LockedUntil.Value.ToLocalTime():HH:mm} sonrasında tekrar deneyin.");

        var valid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        if (!valid)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockedUntil = DateTime.UtcNow.Add(LockDuration);
                user.FailedLoginCount = 0;
                await _users.UpdateAsync(user);
                return AuthResult.Fail("Çok fazla hatalı deneme. Hesap 15 dakika kilitlendi.");
            }
            await _users.UpdateAsync(user);
            return AuthResult.Fail("Kullanıcı adı veya şifre hatalı.");
        }

        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        await _users.UpdateAsync(user);
        return AuthResult.Ok(user);
    }

    // Giriş ekranındaki "Şifremi unuttum" akışı.
    //
    // ÖNEMLİ: Girilen kullanıcı adı sistemde olsun ya da olmasın bu metot her zaman
    // aynı şekilde döner. Aksi halde giriş ekranı, hangi kullanıcı adlarının var
    // olduğunu sızdıran bir araca dönüşürdü (kullanıcı adı keşfi / enumeration).
    // Bulunamayan kullanıcı adı da kaydedilir: yazım hatası olabilir ve Admin bunu
    // görüp kullanıcıya doğrusunu söyleyebilir.
    public async Task RequestPasswordResetAsync(string username)
    {
        if (_resetRequests is null) return;

        username = (username ?? string.Empty).Trim();
        if (username.Length == 0) return;

        var lastRequestedAt = await _resetRequests.GetLastRequestAtAsync(username);
        if (lastRequestedAt is not null && DateTime.Now - lastRequestedAt.Value < ResetRequestCooldown)
            return; // hız sınırı — kullanıcıya yine aynı mesaj gösterilir

        var user = await _users.GetByUsernameAsync(username);

        await _resetRequests.AddAsync(new PasswordResetRequest
        {
            Username = username,
            UserId = user?.Id,
            RequestedAt = DateTime.Now,
        });

        await NotifyAdminsOfResetRequestAsync(username, user);
    }

    // Admin'in talebi fark etmesi için Kullanıcı Yönetimi ekranını açmasını beklemek
    // yerine zil bildirimi gönderilir. Bildirim üretilemezse talep yine kaydedilmiş
    // olur — bu yüzden hatalar yutuluyor.
    private async Task NotifyAdminsOfResetRequestAsync(string username, User? matchedUser)
    {
        if (_notifications is null) return;

        try
        {
            var admins = await _users.GetAllAsync();
            var adminIds = admins
                .Where(u => u.Role == UserRole.Admin && !u.IsDisabled)
                .Select(u => u.Id)
                .ToList();

            if (adminIds.Count == 0) return;

            var message = matchedUser is null
                ? $"\"{username}\" kullanıcı adıyla şifre sıfırlama talebi geldi, ancak bu kullanıcı adı sistemde bulunamadı."
                : $"{matchedUser.FullName} ({username}) şifre sıfırlama talebinde bulundu.";

            var notifications = adminIds.Select(id => new Notification
            {
                UserId = id,
                ContractId = null,
                Type = NotificationType.SifreSifirlamaTalebi,
                Title = "Şifre sıfırlama talebi",
                Message = message,
                CreatedAt = DateTime.Now,
            }).ToList();

            await _notifications.AddManyAsync(notifications);
        }
        catch
        {
            // Bildirim kritik olmayan bir yan etki; talep zaten kaydedildi.
        }
    }

    // Kullanıcının kendi şifresini değiştirmesi. Admin'in şifre sıfırlamasından farkı,
    // mevcut şifrenin doğrulanması ve yeni şifreyi kimsenin bilmemesidir.
    public async Task<AuthResult> ChangeOwnPasswordAsync(User currentUser, string currentPassword, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            return AuthResult.Fail("Yeni şifre en az 6 karakter olmalıdır.");

        if (currentPassword == newPassword)
            return AuthResult.Fail("Yeni şifre, mevcut şifreyle aynı olamaz.");

        // Oturumdaki kopya yerine veritabanındaki güncel kaydı okuyoruz: kullanıcı başka
        // bir yerden şifresini değiştirmiş veya hesabı devre dışı bırakılmış olabilir.
        var user = await _users.GetByIdAsync(currentUser.Id);
        if (user is null)
            return AuthResult.Fail("Kullanıcı bulunamadı.");

        if (user.IsDisabled)
            return AuthResult.Fail("Bu hesap devre dışı bırakılmış.");

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            return AuthResult.Fail("Mevcut şifreniz hatalı.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        await _users.UpdateAsync(user);

        // Oturumdaki nesne de güncellenir; aksi halde aynı oturumda ikinci kez şifre
        // değiştirmeye çalışıldığında "mevcut şifre" kontrolü eski hash'e bakardı.
        currentUser.PasswordHash = user.PasswordHash;

        return AuthResult.Ok(user);
    }
}

public class AuthResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public User? User { get; init; }

    public static AuthResult Ok(User user) => new() { Success = true, User = user };
    public static AuthResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
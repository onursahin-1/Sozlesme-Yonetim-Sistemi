using Sys.Domain;

namespace Sys.Services;

public class AuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordResetRequestRepository? _resetRequests;
    private readonly INotificationRepository? _notifications;
    private readonly IAuditLogRepository? _auditLogs;
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    // Aynı kullanıcı adı için bu süre içinde ikinci bir talep oluşturulmaz; aksi halde
    // giriş ekranından Admin'in listesi kolayca doldurulabilirdi.
    private static readonly TimeSpan ResetRequestCooldown = TimeSpan.FromMinutes(10);

    public AuthService(
        IUserRepository users,
        IPasswordResetRequestRepository? resetRequests = null,
        INotificationRepository? notifications = null,
        IAuditLogRepository? auditLogs = null)
    {
        _users = users;
        _resetRequests = resetRequests;
        _notifications = notifications;
        _auditLogs = auditLogs;
    }

    // Kimlik doğrulamayla ilgili güvenlik olayları denetim kaydına yazılır.
    // Kayıt yazılamazsa giriş/şifre işlemi geçerli sayılmaya devam eder.
    private async Task LogAsync(int userId, string action, string detail)
    {
        if (_auditLogs is null) return;
        try
        {
            await _auditLogs.AddAsync(new AuditLog
            {
                EntityName = "User",
                EntityId = userId,
                Action = action,
                ActingUserId = userId,
                Detail = detail,
                ActionDate = DateTime.Now,
            });
        }
        catch
        {
            // Sessizce geç.
        }
    }

    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        var user = await _users.GetByUsernameAsync(username);
        if (user is null)
            return AuthResult.Fail(AppError.InvalidCredentials);

        if (user.IsDisabled)
            return AuthResult.Fail(AppError.AccountDisabled);

        if (user.LockedUntil is not null && user.LockedUntil > DateTime.UtcNow)
            return AuthResult.Fail(AppError.AccountLocked, user.LockedUntil.Value.ToLocalTime().ToString("HH:mm"));

        var valid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        if (!valid)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockedUntil = DateTime.UtcNow.Add(LockDuration);
                user.FailedLoginCount = 0;
                await _users.UpdateAsync(user);

                // Her hatalı deneme değil, yalnızca kilitlenme kaydedilir: tek tek
                // denemeler denetim kaydını gereksiz yere doldururdu, kilitlenme ise
                // incelemeye değer bir güvenlik olayıdır.
                await LogAsync(user.Id, "HesapKilitlendi",
                    $"{user.FullName} ({user.Username}) — {MaxFailedAttempts} hatalı giriş denemesi sonrası {LockDuration.TotalMinutes:0} dakika kilitlendi.");

                return AuthResult.Fail(AppError.TooManyAttempts);
            }
            await _users.UpdateAsync(user);
            return AuthResult.Fail(AppError.InvalidCredentials);
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

            var messageKey = matchedUser is null ? "Ntf.PasswordResetUnknown" : "Ntf.PasswordReset";
            var args = matchedUser is null
                ? NotificationArgs.Serialize([username])
                : NotificationArgs.Serialize([matchedUser.FullName, username]);

            var notifications = adminIds.Select(id => new Notification
            {
                UserId = id,
                ContractId = null,
                Type = NotificationType.SifreSifirlamaTalebi,
                TitleKey = "Ntf.PasswordResetTitle",
                MessageKey = messageKey,
                MessageArgs = args,
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
        // Kural PasswordPolicy'de; yönetici yollarıyla aynı kaynaktan besleniyor.
        // Eskiden buradaki "en az 6 karakter" kontrolü tek başınaydı ve yönetici
        // şifre belirlerken hiç çalışmıyordu.
        if (PasswordPolicy.Validate(newPassword) is { } policyError)
            return AuthResult.Fail(policyError);

        if (currentPassword == newPassword)
            return AuthResult.Fail(AppError.NewPasswordSameAsCurrent);

        // Oturumdaki kopya yerine veritabanındaki güncel kaydı okuyoruz: kullanıcı başka
        // bir yerden şifresini değiştirmiş veya hesabı devre dışı bırakılmış olabilir.
        var user = await _users.GetByIdAsync(currentUser.Id);
        if (user is null)
            return AuthResult.Fail(AppError.UserNotFound);

        if (user.IsDisabled)
            return AuthResult.Fail(AppError.AccountDisabled);

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            return AuthResult.Fail(AppError.CurrentPasswordIncorrect);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.FailedLoginCount = 0;
        user.LockedUntil = null;

        // Şifreyi artık yalnızca kullanıcı biliyor; zorunlu değişim borcu kapandı.
        user.MustChangePassword = false;
        user.PasswordChangedAt = DateTime.Now;
        await _users.UpdateAsync(user);

        // Oturumdaki nesne de güncellenir; aksi halde aynı oturumda ikinci kez şifre
        // değiştirmeye çalışıldığında "mevcut şifre" kontrolü eski hash'e bakardı.
        // Bayrak da taşınmalı: kabuk ekranı bu nesneye bakarak kullanıcıyı serbest
        // bırakıyor, güncellenmezse kullanıcı şifresini değiştirdiği halde zorunlu
        // değişim ekranında kilitli kalırdı.
        currentUser.PasswordHash = user.PasswordHash;
        currentUser.MustChangePassword = false;
        currentUser.PasswordChangedAt = user.PasswordChangedAt;

        await LogAsync(user.Id, "ŞifreDeğiştirildi",
            $"{user.FullName} ({user.Username}) kendi şifresini değiştirdi.");

        return AuthResult.Ok(user);
    }
}

public class AuthResult
{
    public bool Success { get; init; }
    public User? User { get; init; }

    // Başarısızlık sebebi METİN değil KOD.
    //
    // Eskiden ErrorMessage vardı ve arayüz onun İÇİNDE kelime arayarak hatayı
    // hangi alanın altına yazacağına karar veriyordu:
    //     if (result.ErrorMessage.Contains("Mevcut şifreniz")) ...
    // Metin çevrildiği an bu koşul sessizce tutmaz olurdu.
    public AppError? Error { get; init; }
    public object?[] ErrorArgs { get; init; } = [];

    public static AuthResult Ok(User user) => new() { Success = true, User = user };

    public static AuthResult Fail(AppError error, params object?[] args)
        => new() { Success = false, Error = error, ErrorArgs = args };
}
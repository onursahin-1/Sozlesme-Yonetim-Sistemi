using Sys.Domain;
using Sys.Services;

namespace Sys.Services.Tests;

// Kimlik doğrulama davranışları. Bunlar güvenlik kuralları: test edilmezlerse
// ileride biri sessizce bozar ve kimse fark etmez.
public class AuthServiceTests
{
    private const string Password = "DogruSifre1";

    private static (AuthService Service, FakeUserRepository Users,
                    FakePasswordResetRequestRepository Resets,
                    FakeNotificationRepository Notifications,
                    FakeAuditLogRepository Audit) Create(params User[] users)
    {
        var userRepo = new FakeUserRepository();
        foreach (var u in users) userRepo.Users.Add(u);

        var resets = new FakePasswordResetRequestRepository();
        var notifications = new FakeNotificationRepository();
        var audit = new FakeAuditLogRepository();

        return (new AuthService(userRepo, resets, notifications, audit),
                userRepo, resets, notifications, audit);
    }

    private static User Account(string username = "ayse", bool disabled = false,
                                int failedCount = 0, DateTime? lockedUntil = null)
        => new()
        {
            Id = 1,
            Username = username,
            FullName = "Ayşe Yılmaz",
            Role = UserRole.SYB,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
            IsDisabled = disabled,
            FailedLoginCount = failedCount,
            LockedUntil = lockedUntil,
        };

    // ---- Giriş ----

    [Fact]
    public async Task LoginAsync_CorrectPassword_Succeeds()
    {
        var (service, _, _, _, _) = Create(Account());

        var result = await service.LoginAsync("ayse", Password);

        Assert.True(result.Success);
        Assert.Equal("ayse", result.User!.Username);
    }

    // Var olmayan kullanıcı ile yanlış şifre AYNI mesajı vermeli. Farklı mesaj
    // verilseydi giriş ekranı, hangi kullanıcı adlarının var olduğunu sızdıran
    // bir araca dönüşürdü.
    [Fact]
    public async Task LoginAsync_UnknownUserAndWrongPassword_GiveSameMessage()
    {
        var (service, _, _, _, _) = Create(Account());

        var unknown = await service.LoginAsync("olmayan", "herhangi1");
        var wrongPassword = await service.LoginAsync("ayse", "YanlisSifre1");

        Assert.False(unknown.Success);
        Assert.False(wrongPassword.Success);
        Assert.Equal(unknown.ErrorMessage, wrongPassword.ErrorMessage);
    }

    [Fact]
    public async Task LoginAsync_DisabledAccount_Fails()
    {
        var (service, _, _, _, _) = Create(Account(disabled: true));

        var result = await service.LoginAsync("ayse", Password);

        Assert.False(result.Success);
    }

    // ---- Hesap kilitleme ----

    // Beşinci hatalı denemede hesap kilitlenir ve sayaç sıfırlanır; kilit
    // zamanı geleceğe atanır.
    [Fact]
    public async Task LoginAsync_FifthWrongAttempt_LocksAccount()
    {
        var (service, users, _, _, _) = Create(Account());

        for (var i = 0; i < 5; i++)
            await service.LoginAsync("ayse", "YanlisSifre1");

        var account = users.Users[0];
        Assert.NotNull(account.LockedUntil);
        Assert.True(account.LockedUntil > DateTime.UtcNow);
        Assert.Equal(0, account.FailedLoginCount);
    }

    [Fact]
    public async Task LoginAsync_FourWrongAttempts_DoesNotLock()
    {
        var (service, users, _, _, _) = Create(Account());

        for (var i = 0; i < 4; i++)
            await service.LoginAsync("ayse", "YanlisSifre1");

        Assert.Null(users.Users[0].LockedUntil);
        Assert.Equal(4, users.Users[0].FailedLoginCount);
    }

    // Kilitliyken DOĞRU şifre bile kabul edilmemeli; aksi halde kilitlemenin
    // hiçbir anlamı kalmaz.
    [Fact]
    public async Task LoginAsync_LockedAccount_RejectsEvenCorrectPassword()
    {
        var (service, _, _, _, _) = Create(Account(lockedUntil: DateTime.UtcNow.AddMinutes(10)));

        var result = await service.LoginAsync("ayse", Password);

        Assert.False(result.Success);
    }

    // Kilit süresi geçmişse hesap kendiliğinden açılır.
    [Fact]
    public async Task LoginAsync_ExpiredLock_AllowsLogin()
    {
        var (service, users, _, _, _) = Create(Account(lockedUntil: DateTime.UtcNow.AddMinutes(-1)));

        var result = await service.LoginAsync("ayse", Password);

        Assert.True(result.Success);
        Assert.Null(users.Users[0].LockedUntil);
    }

    // Araya başarılı bir giriş girerse sayaç sıfırlanmalı: hatalı denemeler
    // ardışık olmadığında kilitlemeye sayılmaz.
    [Fact]
    public async Task LoginAsync_SuccessResetsFailedCount()
    {
        var (service, users, _, _, _) = Create(Account());

        await service.LoginAsync("ayse", "YanlisSifre1");
        await service.LoginAsync("ayse", "YanlisSifre1");
        await service.LoginAsync("ayse", Password);

        Assert.Equal(0, users.Users[0].FailedLoginCount);
    }

    // Tek tek denemeler değil, yalnızca KİLİTLENME denetime yazılır: her hatalı
    // deneme kaydedilseydi denetim kaydı gereksiz yere dolardı.
    [Fact]
    public async Task LoginAsync_LogsOnlyTheLockEvent()
    {
        var (service, _, _, _, audit) = Create(Account());

        for (var i = 0; i < 5; i++)
            await service.LoginAsync("ayse", "YanlisSifre1");

        Assert.Single(audit.Logs);
        Assert.Equal("HesapKilitlendi", audit.Logs[0].Action);
    }
}

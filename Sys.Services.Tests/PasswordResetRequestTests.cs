using Sys.Domain;
using Sys.Services;

namespace Sys.Services.Tests;

// Giriş ekranındaki "Şifremi unuttum" akışı.
//
// Buradaki asıl kural gizlilik: girilen kullanıcı adı sistemde olsun ya da
// olmasın akış AYNI şekilde işlemeli. Aksi halde giriş ekranı, hangi kullanıcı
// adlarının var olduğunu sızdıran bir araca dönüşür.
public class PasswordResetRequestTests
{
    private static (AuthService Service, FakePasswordResetRequestRepository Resets,
                    FakeNotificationRepository Notifications) Create(params User[] users)
    {
        var userRepo = new FakeUserRepository();
        foreach (var u in users) userRepo.Users.Add(u);

        var resets = new FakePasswordResetRequestRepository();
        var notifications = new FakeNotificationRepository();

        return (new AuthService(userRepo, resets, notifications), resets, notifications);
    }

    private static User Known() => new()
    {
        Id = 1, Username = "ayse", FullName = "Ayşe Yılmaz", Role = UserRole.SYB,
    };

    private static User Admin(int id = 9, bool disabled = false) => new()
    {
        Id = id, Username = "admin", FullName = "Yönetici", Role = UserRole.Admin,
        IsDisabled = disabled,
    };

    [Fact]
    public async Task RequestPasswordResetAsync_KnownUser_RecordsRequestWithUserId()
    {
        var (service, resets, _) = Create(Known());

        await service.RequestPasswordResetAsync("ayse");

        Assert.Single(resets.Requests);
        Assert.Equal(1, resets.Requests[0].UserId);
    }

    // Bulunamayan kullanıcı adı da KAYDEDİLİR: yazım hatası olabilir ve Admin
    // bunu görüp kullanıcıya doğrusunu söyleyebilir. Kayıt oluşmasaydı, talebin
    // kaydedilip kaydedilmemesi kullanıcı adının varlığını ele verirdi.
    [Fact]
    public async Task RequestPasswordResetAsync_UnknownUser_StillRecordsRequest()
    {
        var (service, resets, _) = Create(Known());

        await service.RequestPasswordResetAsync("olmayan");

        Assert.Single(resets.Requests);
        Assert.Null(resets.Requests[0].UserId);
        Assert.Equal("olmayan", resets.Requests[0].Username);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_TrimsUsername()
    {
        var (service, resets, _) = Create(Known());

        await service.RequestPasswordResetAsync("  ayse  ");

        Assert.Equal("ayse", resets.Requests[0].Username);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_BlankUsername_DoesNothing()
    {
        var (service, resets, _) = Create(Known());

        await service.RequestPasswordResetAsync("   ");

        Assert.Empty(resets.Requests);
    }

    // Hız sınırı: aynı kullanıcı adı için 10 dakika içinde ikinci talep
    // oluşturulmaz. Aksi halde giriş ekranından Admin'in listesi kolayca
    // doldurulabilirdi.
    [Fact]
    public async Task RequestPasswordResetAsync_WithinCooldown_Skipped()
    {
        var (service, resets, _) = Create(Known());
        resets.LastRequestAt = DateTime.Now.AddMinutes(-2);

        await service.RequestPasswordResetAsync("ayse");

        Assert.Empty(resets.Requests);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_AfterCooldown_Recorded()
    {
        var (service, resets, _) = Create(Known());
        resets.LastRequestAt = DateTime.Now.AddMinutes(-11);

        await service.RequestPasswordResetAsync("ayse");

        Assert.Single(resets.Requests);
    }

    // ---- Admin bildirimi ----

    [Fact]
    public async Task RequestPasswordResetAsync_NotifiesActiveAdminsOnly()
    {
        var (service, _, notifications) = Create(Known(), Admin(9), Admin(10, disabled: true));

        await service.RequestPasswordResetAsync("ayse");

        Assert.Single(notifications.Added);
        Assert.Equal(9, notifications.Added[0].UserId);
        Assert.Equal(NotificationType.SifreSifirlamaTalebi, notifications.Added[0].Type);
    }

    // Bilinmeyen kullanıcı adında bildirim farklı olur — bu Admin'e
    // yöneliktir, giriş ekranındaki kullanıcıya değil. Gizlilik kuralı giriş
    // ekranı için geçerli; Admin zaten sistemi yönetiyor.
    [Fact]
    public async Task RequestPasswordResetAsync_UnknownUser_AdminMessageSaysNotFound()
    {
        var (service, _, notifications) = Create(Known(), Admin());

        await service.RequestPasswordResetAsync("olmayan");

        // Metin değil ANAHTAR sınanıyor: bildirim artık üretildiği anda metne
        // dönüşmüyor, okunduğu anda okuyanın dilinde kuruluyor.
        Assert.Equal("Ntf.PasswordResetUnknown", notifications.Added[0].MessageKey);
    }

    // Bildirim üretilemese bile talep kaydedilmiş olmalı: bildirim kritik
    // olmayan bir yan etki.
    [Fact]
    public async Task RequestPasswordResetAsync_NoAdmins_StillRecordsRequest()
    {
        var (service, resets, notifications) = Create(Known());

        await service.RequestPasswordResetAsync("ayse");

        Assert.Single(resets.Requests);
        Assert.Empty(notifications.Added);
    }
}

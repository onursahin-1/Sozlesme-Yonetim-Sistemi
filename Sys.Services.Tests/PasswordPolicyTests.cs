using Sys.Domain;
using Sys.Services;

namespace Sys.Services.Tests;

public class PasswordPolicyTests
{
    // ---- Kuralın kendisi ----

    [Theory]
    [InlineData("Sifre123")]
    [InlineData("a1b2c3d4")]
    [InlineData("uzun bir sifre 2026")]
    public void Validate_ValidPasswords_ReturnsNull(string password)
        => Assert.Null(PasswordPolicy.Validate(password));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Sifre1")]      // 6 karakter — eski kural bunu kabul ediyordu
    [InlineData("sifresiz")]    // rakam yok
    [InlineData("12345678")]    // harf yok
    public void Validate_InvalidPasswords_ReturnsMessage(string? password)
        => Assert.NotNull(PasswordPolicy.Validate(password));

    // Kullanıcı adını veya ad-soyadı içermek serbest: kural bilinçli olarak
    // konulmadı, kullanıcıyı gereksiz yere zorlamamak için.
    [Fact]
    public void Validate_ContainsUsername_IsAllowed()
        => Assert.Null(PasswordPolicy.Validate("ayse.yilmaz2026"));

    // ---- Yönetici yolları ----
    //
    // Bu iki yol eskiden HİÇBİR doğrulama yapmıyordu: yönetici yeni kullanıcıya
    // "1" şifresini verebiliyordu, oysa kullanıcının kendisi "12345" bile yapamıyordu.

    private static (UserManagementService Service, FakeUserRepository Repo) CreateUserService()
    {
        var repo = new FakeUserRepository();
        return (new UserManagementService(repo), repo);
    }

    private static User Admin() => new() { Id = 99, Role = UserRole.Admin, Username = "admin" };

    [Fact]
    public async Task CreateUserAsync_WeakPassword_Throws()
    {
        var (service, _) = CreateUserService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateUserAsync(Admin(), "ayse", "Ayşe Yılmaz", UserRole.SYB, null, "1"));
    }

    // Yöneticinin belirlediği şifreyi iki kişi biliyor; kullanıcı ilk girişinde
    // değiştirmek zorunda olmalı ki denetim kaydı gerçekten onu göstersin.
    [Fact]
    public async Task CreateUserAsync_SetsMustChangePassword()
    {
        var (service, _) = CreateUserService();

        var created = await service.CreateUserAsync(
            Admin(), "ayse", "Ayşe Yılmaz", UserRole.SYB, null, "Gecici123");

        Assert.True(created.MustChangePassword);
    }

    [Fact]
    public async Task ResetPasswordAsync_WeakPassword_Throws()
    {
        var (service, repo) = CreateUserService();
        await repo.AddAsync(new User { Username = "ayse", Role = UserRole.SYB });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ResetPasswordAsync(Admin(), 1, "abc"));
    }

    [Fact]
    public async Task ResetPasswordAsync_SetsMustChangePassword()
    {
        var (service, repo) = CreateUserService();
        await repo.AddAsync(new User { Username = "ayse", Role = UserRole.SYB });

        await service.ResetPasswordAsync(Admin(), 1, "Gecici123");

        Assert.True(repo.Users[0].MustChangePassword);
    }

    // ---- Kullanıcının kendi değişikliği ----

    // Dönen User, depodakinin AYRI bir kopyası: gerçekte de oturumdaki nesne ile
    // veritabanından okunan kayıt farklı örneklerdir. Aynı nesne verilseydi
    // "oturumdaki kopya da güncelleniyor mu" testi kendiliğinden geçerdi.
    private static (AuthService Service, FakeUserRepository Repo, User SessionUser) CreateAuthService(
        string currentPassword, bool mustChange = false)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(currentPassword);
        var repo = new FakeUserRepository();

        repo.Users.Add(new User
        {
            Id = 1,
            Username = "ayse",
            FullName = "Ayşe Yılmaz",
            Role = UserRole.SYB,
            PasswordHash = hash,
            MustChangePassword = mustChange,
        });

        var sessionUser = new User
        {
            Id = 1,
            Username = "ayse",
            FullName = "Ayşe Yılmaz",
            Role = UserRole.SYB,
            PasswordHash = hash,
            MustChangePassword = mustChange,
        };

        return (new AuthService(repo), repo, sessionUser);
    }

    [Fact]
    public async Task ChangeOwnPasswordAsync_WeakPassword_Fails()
    {
        var (service, _, sessionUser) = CreateAuthService("EskiSifre1");

        var result = await service.ChangeOwnPasswordAsync(sessionUser, "EskiSifre1", "kisa1");

        Assert.False(result.Success);
    }

    // Zorunlu değişim borcu, kullanıcı kendi şifresini belirlediğinde kapanır.
    [Fact]
    public async Task ChangeOwnPasswordAsync_ClearsMustChangePassword()
    {
        var (service, repo, sessionUser) = CreateAuthService("Gecici123", mustChange: true);

        var result = await service.ChangeOwnPasswordAsync(sessionUser, "Gecici123", "YeniSifre1");

        Assert.True(result.Success);
        Assert.False(repo.Users[0].MustChangePassword);
        Assert.NotNull(repo.Users[0].PasswordChangedAt);

        // Oturumdaki nesne de güncellenmeli; aksi halde kullanıcı şifresini
        // değiştirdiği halde zorunlu değişim ekranında kilitli kalırdı.
        Assert.False(sessionUser.MustChangePassword);
    }

    // ---- Mevcut kullanıcılar etkilenmemeli ----
    //
    // Politika yalnızca şifre BELİRLENİRKEN çalışır, girişte değil. Eski ve artık
    // kurala uymayan bir şifreyle giriş yapılabilmeye devam etmeli; aksi halde bu
    // değişiklik mevcut kullanıcıları sistemden kilitlerdi.
    [Fact]
    public async Task LoginAsync_LegacyWeakPassword_StillWorks()
    {
        var (service, _, _) = CreateAuthService("abc123"); // 6 karakter, eski kurala göre geçerli

        var result = await service.LoginAsync("ayse", "abc123");

        Assert.True(result.Success);
    }

    // Mevcut kullanıcılar zorunlu değişime düşmez: alan varsayılan olarak false.
    [Fact]
    public void ExistingUser_DefaultsToNoForcedChange()
        => Assert.False(new User().MustChangePassword);
}

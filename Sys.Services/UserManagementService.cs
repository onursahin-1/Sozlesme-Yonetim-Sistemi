using Sys.Domain;

namespace Sys.Services;

// Kullanıcı yönetimi (yeni kullanıcı, şifre sıfırlama, hesap devre dışı bırakma) yalnızca
// Admin rolündeki hesaplara açıktır — her metod bunu kontrol eder. Admin, iş süreçlerine
// (sözleşme onayı vb.) karışmayan, yalnızca hesap yönetiminden sorumlu ayrı bir roldür.
public class UserManagementService
{
    private readonly IUserRepository _users;

    public UserManagementService(IUserRepository users)
    {
        _users = users;
    }

    private static void EnsureAdmin(User actingUser)
    {
        if (actingUser.Role != UserRole.Admin)
            throw new InvalidOperationException("Bu işlemi yapma yetkiniz yok.");
    }

    public async Task<List<User>> GetAllUsersAsync(User actingUser)
    {
        EnsureAdmin(actingUser);
        return await _users.GetAllAsync();
    }

    public async Task<User> CreateUserAsync(User actingUser, string username, string fullName, UserRole role, string? department, string initialPassword)
    {
        EnsureAdmin(actingUser);

        var user = new User
        {
            Username = username,
            FullName = fullName,
            Role = role,
            Department = department,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(initialPassword),
        };

        await _users.AddAsync(user);
        return user;
    }

    public async Task ResetPasswordAsync(User actingUser, int userId, string newPassword)
    {
        EnsureAdmin(actingUser);

        var user = await _users.GetByIdAsync(userId);
        if (user is null)
            throw new InvalidOperationException("Kullanıcı bulunamadı.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        // Şifre sıfırlanınca önceki hatalı giriş kilidi de temizlenir; aksi halde
        // kullanıcı doğru yeni şifreyle bile hâlâ kilitli kalabilir.
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        await _users.UpdateAsync(user);
    }

    public async Task<User> SetDisabledAsync(User actingUser, int userId, bool disabled)
    {
        EnsureAdmin(actingUser);

        if (disabled && actingUser.Id == userId)
            throw new InvalidOperationException("Kendi hesabınızı devre dışı bırakamazsınız.");

        var user = await _users.GetByIdAsync(userId);
        if (user is null)
            throw new InvalidOperationException("Kullanıcı bulunamadı.");

        user.IsDisabled = disabled;
        await _users.UpdateAsync(user);
        return user;
    }
}
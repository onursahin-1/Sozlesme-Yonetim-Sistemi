using Sys.Domain;

namespace Sys.Services;

// Kullanıcı yönetimi (yeni kullanıcı, şifre sıfırlama, hesap devre dışı bırakma) yalnızca
// Admin rolündeki hesaplara açıktır — her metod bunu kontrol eder. Admin, iş süreçlerine
// (sözleşme onayı vb.) karışmayan, yalnızca hesap yönetiminden sorumlu ayrı bir roldür.
public class UserManagementService
{
    private readonly IUserRepository _users;
    private readonly IPasswordResetRequestRepository? _resetRequests;

    public UserManagementService(IUserRepository users, IPasswordResetRequestRepository? resetRequests = null)
    {
        _users = users;
        _resetRequests = resetRequests;
    }

    // Giriş ekranından gelen, henüz karşılanmamış şifre sıfırlama talepleri.
    public async Task<List<PasswordResetRequest>> GetPendingResetRequestsAsync(User actingUser)
    {
        EnsureAdmin(actingUser);
        if (_resetRequests is null) return new List<PasswordResetRequest>();
        return await _resetRequests.GetPendingAsync();
    }

    // Talebi karşılamadan kapatmak için (örn. kullanıcı adı hatalı girilmiş, yapılacak
    // bir şey yok ya da kullanıcıyla başka bir yoldan hallolmuş).
    public async Task DismissResetRequestAsync(User actingUser, int requestId)
    {
        EnsureAdmin(actingUser);
        if (_resetRequests is null) return;
        await _resetRequests.MarkHandledAsync(requestId, actingUser.Id);
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

        // Bu kullanıcının bekleyen sıfırlama talepleri artık karşılanmış sayılır;
        // Admin'in listeden elle temizlemesi gerekmesin.
        if (_resetRequests is not null)
            await _resetRequests.MarkHandledForUserAsync(user.Id, actingUser.Id);
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
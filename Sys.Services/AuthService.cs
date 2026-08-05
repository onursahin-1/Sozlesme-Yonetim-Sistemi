using Sys.Domain;

namespace Sys.Services;

public class AuthService
{
    private readonly IUserRepository _users;
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    public AuthService(IUserRepository users)
    {
        _users = users;
    }

    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        var user = await _users.GetByUsernameAsync(username);
        if (user is null)
            return AuthResult.Fail("Kullanıcı adı veya şifre hatalı.");

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
}

public class AuthResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public User? User { get; init; }

    public static AuthResult Ok(User user) => new() { Success = true, User = user };
    public static AuthResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
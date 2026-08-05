namespace Sys.Domain;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }

    // Güvenlik: hesap kilitleme
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntil { get; set; }
}

public enum UserRole
{
    Personel,
    SYB,
    Mudur
}
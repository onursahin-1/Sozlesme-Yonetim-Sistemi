namespace Sys.Domain;

public class AuditLog
{
    public int Id { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public int ActingUserId { get; set; }
    public string? Detail { get; set; }
    public DateTime ActionDate { get; set; }
    public User? ActingUser { get; set; }
}
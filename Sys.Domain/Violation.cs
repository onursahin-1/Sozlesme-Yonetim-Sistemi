using System;

namespace Sys.Domain;

public class Violation
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public string ViolationType { get; set; } = string.Empty;
    public DateTime ViolationDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public int ReportedByUserId { get; set; }
    public DateTime ReportedAt { get; set; }
}
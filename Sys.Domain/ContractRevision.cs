using System;

namespace Sys.Domain;

public class ContractRevision
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal PreviousTotalAmount { get; set; }
    public DateTime? PreviousEndDate { get; set; }
    public string PreviousDescription { get; set; } = string.Empty;
    public int ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; }
}
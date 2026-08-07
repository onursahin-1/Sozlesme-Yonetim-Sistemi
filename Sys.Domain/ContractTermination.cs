using System;

namespace Sys.Domain;

public class ContractTermination
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public string TerminationType { get; set; } = string.Empty;
    public DateTime TerminationDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal? CompensationAmount { get; set; }
    public string CompensationDirection { get; set; } = string.Empty;
    public int RequestedByUserId { get; set; }
    public DateTime RequestedAt { get; set; }
}
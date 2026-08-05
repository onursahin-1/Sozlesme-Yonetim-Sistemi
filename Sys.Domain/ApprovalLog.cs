namespace Sys.Domain;

public class ApprovalLog
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public int StepNumber { get; set; }
    public string StepName { get; set; } = string.Empty;
    public int ActingUserId { get; set; }
    public ApprovalDecision Decision { get; set; }
    public string? Note { get; set; }
    public DateTime ActionDate { get; set; }
}

public enum ApprovalDecision
{
    Onay,
    Red
}
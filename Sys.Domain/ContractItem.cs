namespace Sys.Domain;

public class ContractItem
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
}
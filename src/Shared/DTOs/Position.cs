namespace Shared.DTOs;

/// <summary>
/// Represents a position on a contract for a customer/book. Positions are typically an aggregation of trades.
/// </summary>
public class Position
{
    public long? PositionId { get; set; }
    public int ContractId { get; set; }
    public int CustomerId { get; set; }
    public int BookId { get; set; }
    public decimal TraderId { get; set; }
    public int DepartmentId { get; set; }
    public DateTime TimeUpdated { get; set; }
    public decimal Volume { get; set; }
    public string ProductType { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

namespace Shared.DTOs;

/// <summary>
/// Represents a trade (future, swap, etc.) used to derive positions and P&L.
/// </summary>
public class Trade
{
    public long TradeId { get; set; }
    public int ContractId { get; set; }
    public int CustomerId { get; set; }
    public int BookId { get; set; }
    public decimal TraderId { get; set; }
    public int DepartmentId { get; set; }
    public DateTime TradeDate { get; set; }
    public DateTime TimeUpdated { get; set; }
    public decimal Volume { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public int? CounterpartyId { get; set; }
    public DateTime? DeliveryStart { get; set; }
    public DateTime? DeliveryEnd { get; set; }
    public string ProductType { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

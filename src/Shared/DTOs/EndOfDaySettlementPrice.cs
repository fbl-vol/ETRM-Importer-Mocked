namespace Shared.DTOs;

/// <summary>
/// End-of-day settlement price for a contract/customer at a given trading period.
/// </summary>
public class EndOfDaySettlementPrice
{
    public int ContractId { get; set; }
    public int CustomerId { get; set; }
    public DateTime TradingPeriod { get; set; }
    public DateTime PublicationTime { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string PriceSource { get; set; } = string.Empty;
    public string MarketZone { get; set; } = string.Empty;
}

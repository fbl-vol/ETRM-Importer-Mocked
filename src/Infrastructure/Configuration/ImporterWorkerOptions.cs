namespace Infrastructure.Configuration;

/// <summary>
/// Configuration options for the ImporterWorker to control trade and price generation.
/// </summary>
public class ImporterWorkerOptions
{
    /// <summary>
    /// Minimum interval in seconds between trade batch generations.
    /// </summary>
    public int MinTradeIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum interval in seconds between trade batch generations.
    /// </summary>
    public int MaxTradeIntervalSeconds { get; set; } = 300;

    /// <summary>
    /// Minimum number of trades per batch.
    /// </summary>
    public int MinTradesPerBatch { get; set; } = 1;

    /// <summary>
    /// Maximum number of trades per batch.
    /// </summary>
    public int MaxTradesPerBatch { get; set; } = 10;

    /// <summary>
    /// Hour of day (UTC) when EOD prices should be published (0-23).
    /// </summary>
    public int EodPricePublishHour { get; set; } = 16;

    /// <summary>
    /// Whether to increase trade frequency during business hours (8:00-17:00 UTC).
    /// </summary>
    public bool UseBusinessHoursPattern { get; set; } = true;

    /// <summary>
    /// Multiplier for trade frequency during business hours (applied to interval).
    /// </summary>
    public double BusinessHoursFrequencyMultiplier { get; set; } = 0.5;
}

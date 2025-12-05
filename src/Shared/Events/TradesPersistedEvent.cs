namespace Shared.Events;

/// <summary>
/// Event published when normalized trades have been persisted to TimescaleDB.
/// </summary>
public class TradesPersistedEvent
{
    public string EventType { get; set; } = "ETRM.Normalized.Trades.Persisted";
    public string ImportId { get; set; } = string.Empty;
    public int Count { get; set; }
    public DateTime PersistedAt { get; set; }
}

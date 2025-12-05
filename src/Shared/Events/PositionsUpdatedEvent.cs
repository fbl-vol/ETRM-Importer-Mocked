namespace Shared.Events;

/// <summary>
/// Event published when positions have been updated/aggregated.
/// </summary>
public class PositionsUpdatedEvent
{
    public string EventType { get; set; } = "ETRM.Positions.Updated";
    public int Count { get; set; }
    public DateTime UpdatedAt { get; set; }
}

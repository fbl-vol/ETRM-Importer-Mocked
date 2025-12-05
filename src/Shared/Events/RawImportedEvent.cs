namespace Shared.Events;

/// <summary>
/// Event published when raw ETRM data has been imported and stored in S3.
/// </summary>
public class RawImportedEvent
{
    public string EventType { get; set; } = "ETRM.Raw.Imported";
    public string ImportId { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime ImportedAt { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

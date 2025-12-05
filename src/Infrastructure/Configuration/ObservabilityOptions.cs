namespace Infrastructure.Configuration;

/// <summary>
/// Configuration options for observability (tracing, metrics, logging).
/// </summary>
public class ObservabilityOptions
{
    public string ServiceName { get; set; } = "ETRM.Importer.Mock";
    public string ServiceVersion { get; set; } = "1.0.0";
    public string OtlpEndpoint { get; set; } = "http://localhost:4317";
    public bool EnableTracing { get; set; } = true;
    public bool EnableMetrics { get; set; } = true;
    public bool EnableLogging { get; set; } = true;
}

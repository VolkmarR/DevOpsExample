namespace Observability;

public class ObservabilityOptions
{
    public bool HttpClient { get; set; } = false;
    public bool Postgres { get; set; } = false;
    public string JaegerUrl { get; set; } = "";
}

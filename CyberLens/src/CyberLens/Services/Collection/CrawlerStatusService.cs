namespace CyberLens.Services.Collection;

/// <summary>
/// Live, in-memory view of the crawler's state, shared across the app so the UI can show
/// whether collection is currently running. Updated by <see cref="CollectorService"/> (per pass)
/// and <see cref="CrawlerService"/> (schedule).
/// </summary>
public class CrawlerStatusService
{
    private readonly object _gate = new();

    public bool Enabled { get; set; } = true;
    public bool IsCollecting { get; private set; }
    public DateTime? LastRunAt { get; private set; }
    public int LastItemsAdded { get; private set; }
    public string LastTrigger { get; private set; } = "-";
    public DateTime? NextRunAt { get; set; }
    public int IntervalSeconds { get; set; } = 45;
    public long TotalPasses { get; private set; }
    public string? CurrentConnector { get; private set; }

    public event Action? Changed;

    public void BeginPass(string trigger)
    {
        lock (_gate) { IsCollecting = true; LastTrigger = trigger; }
        Notify();
    }

    public void SetConnector(string? name)
    {
        lock (_gate) CurrentConnector = name;
        Notify();
    }

    public void EndPass(int itemsAdded)
    {
        lock (_gate)
        {
            IsCollecting = false;
            CurrentConnector = null;
            LastRunAt = DateTime.UtcNow;
            LastItemsAdded = itemsAdded;
            TotalPasses++;
        }
        Notify();
    }

    /// <summary>Overall state for the indicator: running | idle | off.</summary>
    public string State => !Enabled ? "off" : IsCollecting ? "running" : "idle";

    private void Notify() { try { Changed?.Invoke(); } catch { } }
}

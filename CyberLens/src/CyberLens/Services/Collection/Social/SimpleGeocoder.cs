using CyberLens.Data;

namespace CyberLens.Services.Collection.Social;

/// <summary>
/// Lightweight text geocoder: if a post mentions a known city, tag it with that city's
/// coordinates (plus small jitter) so real social/news items still populate the maps and globe.
/// </summary>
public static class SimpleGeocoder
{
    private static readonly Random Rng = new();

    public static (double Lat, double Lon, string Name)? Locate(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var lower = text.ToLowerInvariant();
        foreach (var c in SampleContent.Cities)
        {
            if (lower.Contains(c.Name.ToLowerInvariant()))
                return (c.Lat + (Rng.NextDouble() - 0.5) * 0.2, c.Lon + (Rng.NextDouble() - 0.5) * 0.2, c.Name);
        }
        return null;
    }
}

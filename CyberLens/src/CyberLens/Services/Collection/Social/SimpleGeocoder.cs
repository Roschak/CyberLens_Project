using System.Text.RegularExpressions;

namespace CyberLens.Services.Collection.Social;

/// <summary>
/// Lightweight text geocoder for Indonesian news & OSINT content: detects city/region
/// mentions in articles and tags them with real geographic coordinates.
/// Jitter is deterministic per-text hash so points don't stack on exact coordinates,
/// while remaining stable across re-indexing.
/// </summary>
public static class SimpleGeocoder
{
    private record GeoEntry(string Name, double Lat, double Lon, string[] Aliases);

    private static readonly GeoEntry[] KnownLocations = new[]
    {
        new GeoEntry("Jakarta Pusat", -6.1805, 106.8284, new[] { "jakarta pusat", "monas", "menteng", "gambir", "tanah abang" }),
        new GeoEntry("Jakarta Selatan", -6.2615, 106.8106, new[] { "jakarta selatan", "kuningan", "scbd", "blok m", "fatmawati" }),
        new GeoEntry("DKI Jakarta", -6.2088, 106.8456, new[] { "jakarta", "dki", "jabodetabek" }),
        new GeoEntry("IKN Nusantara, Kaltim", -0.9664, 116.7011, new[] { "ikn", "nusantara", "penajam paser utara", "sepaku" }),
        new GeoEntry("Surabaya, Jawa Timur", -7.2575, 112.7521, new[] { "surabaya", "rungkut", "gubeng" }),
        new GeoEntry("Bandung, Jawa Barat", -6.9175, 107.6191, new[] { "bandung", "gedung sate", "dago", "cimahi" }),
        new GeoEntry("Medan, Sumatera Utara", 3.5952, 98.6722, new[] { "medan", "belawan", "sumut" }),
        new GeoEntry("Semarang, Jawa Tengah", -6.9667, 110.4167, new[] { "semarang", "simpang lima", "jateng" }),
        new GeoEntry("Yogyakarta, DIY", -7.7956, 110.3695, new[] { "yogyakarta", "jogja", "malioboro", "diy", "sleman", "bantul" }),
        new GeoEntry("Makassar, Sulawesi Selatan", -5.1477, 119.4327, new[] { "makassar", "ujung pandang", "losari", "sulsel" }),
        new GeoEntry("Palembang, Sumatera Selatan", -2.9909, 104.7565, new[] { "palembang", "ampera", "sumsel" }),
        new GeoEntry("Denpasar, Bali", -8.6705, 115.2126, new[] { "bali", "denpasar", "kuta", "sanur", "ubud", "badung" }),
        new GeoEntry("Balikpapan, Kalimantan Timur", -1.2379, 116.8529, new[] { "balikpapan", "kaltim" }),
        new GeoEntry("Batam, Kepulauan Riau", 1.1301, 104.0529, new[] { "batam", "kepri", "tanjung pinang" }),
        new GeoEntry("Solo, Jawa Tengah", -7.5755, 110.8243, new[] { "solo", "surakarta" }),
        new GeoEntry("Malang, Jawa Timur", -7.9666, 112.6326, new[] { "malang", "batu" }),
        new GeoEntry("Cirebon, Jawa Barat", -6.7320, 108.5523, new[] { "cirebon" }),
        new GeoEntry("Banten", -6.1104, 106.1640, new[] { "banten", "serang", "cilegon", "tangerang" }),
        new GeoEntry("Pekanbaru, Riau", 0.5071, 101.4478, new[] { "pekanbaru", "riau" }),
        new GeoEntry("Padang, Sumatera Barat", -0.9471, 100.4172, new[] { "padang", "sumbar", "bukittinggi" }),
        new GeoEntry("Banda Aceh, Aceh", 5.5483, 95.3238, new[] { "aceh", "banda aceh" }),
        new GeoEntry("Pontianak, Kalimantan Barat", -0.0263, 109.3425, new[] { "pontianak", "kalbar" }),
        new GeoEntry("Banjarmasin, Kalimantan Selatan", -3.3194, 114.5908, new[] { "banjarmasin", "kalsel" }),
        new GeoEntry("Manado, Sulawesi Utara", 1.4748, 124.8428, new[] { "manado", "sulut", "bunaken" }),
        new GeoEntry("Ambon, Maluku", -3.6547, 128.1906, new[] { "ambon", "maluku" }),
        new GeoEntry("Jayapura, Papua", -2.5489, 140.7186, new[] { "jayapura", "papua", "merauke" }),
    };

    public static (double Lat, double Lon, string Name)? Locate(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var lower = " " + text.ToLowerInvariant() + " ";

        foreach (var loc in KnownLocations)
        {
            foreach (var alias in loc.Aliases)
            {
                // Word boundary check to prevent substring collision (e.g. "ikn" inside "ikan")
                var pattern = $@"\b{Regex.Escape(alias)}\b";
                if (Regex.IsMatch(lower, pattern, RegexOptions.IgnoreCase))
                {
                    // Deterministic jitter based on text hash to prevent exact point overlap
                    var hash = (uint)text.GetHashCode();
                    var jLat = ((hash % 1000) / 1000.0 - 0.5) * 0.04;
                    var jLon = (((hash / 1000) % 1000) / 1000.0 - 0.5) * 0.04;
                    return (Math.Round(loc.Lat + jLat, 4), Math.Round(loc.Lon + jLon, 4), loc.Name);
                }
            }
        }
        return null;
    }
}

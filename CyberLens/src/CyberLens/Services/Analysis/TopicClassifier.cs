namespace CyberLens.Services.Analysis;

/// <summary>Keyword-based automatic categorization (politik, ekonomi, keamanan, ...).</summary>
public static class TopicClassifier
{
    private static readonly (string Category, string[] Terms)[] Rules =
    {
        ("Keamanan", new[] { "ransomware", "phishing", "hacker", "peretas", "kebocoran", "bocor", "siber", "cyber",
            "malware", "exploit", "penipuan", "polri", "bssn", "kriminal", "sindikat", "darkweb", "credential", "breach" }),
        ("Politik", new[] { "pemilu", "partai", "dpr", "koalisi", "kandidat", "menteri", "gubernur", "presiden",
            "parlemen", "parliament", "electoral", "kebijakan", "undang-undang", "mk", "survei politik" }),
        ("Ekonomi", new[] { "rupiah", "inflasi", "harga", "pasar", "umkm", "investasi", "ekspor", "impor", "saham",
            "suku bunga", "bank", "ekonomi", "layoff", "funding", "omzet", "neraca", "dolar", "pajak" }),
        ("Teknologi", new[] { "startup", "ai ", "aplikasi", "internet", "satelit", "digital", "teknologi", "software",
            "kabel bawah laut", "submarine cable", "regulasi ai", "model bahasa", "cloud" }),
        ("Kesehatan", new[] { "vaksin", "rumah sakit", "rsud", "dbd", "kesehatan", "pasien", "telemedicine",
            "dokter", "puskesmas", "wabah", "fogging" }),
        ("Lingkungan", new[] { "banjir", "mangrove", "udara", "emisi", "sampah", "iklim", "cuaca", "hujan",
            "lingkungan", "bus listrik", "energi terbarukan", "pesisir" }),
        ("Sosial", new[] { "relawan", "budaya", "festival", "sekolah", "perundungan", "bullying", "komunitas",
            "warga", "penggusuran", "donasi", "volunteer", "gen z" }),
    };

    /// <summary>Returns the best-matching category name, or null when nothing matches.</summary>
    public static string? Classify(string text)
    {
        var lower = " " + text.ToLowerInvariant() + " ";
        string? best = null;
        var bestScore = 0;
        foreach (var (category, terms) in Rules)
        {
            var score = terms.Count(t => lower.Contains(t));
            if (score > bestScore) { bestScore = score; best = category; }
        }
        return best;
    }
}

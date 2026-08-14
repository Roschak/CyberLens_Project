namespace CyberLens.Services.Analysis;

/// <summary>
/// Fast lexicon-based sentiment analysis for Indonesian and English text,
/// with simple negation handling. Suitable for high-volume streams; the chatbot
/// can be used for deeper AI-based analysis of individual items.
/// </summary>
public static class SentimentAnalyzer
{
    private static readonly HashSet<string> Positive = new(StringComparer.OrdinalIgnoreCase)
    {
        // Indonesian
        "baik", "bagus", "hebat", "sukses", "berhasil", "meningkat", "menguat", "naik", "positif", "aman",
        "apresiasi", "puas", "membaik", "pulih", "rekor", "surplus", "terkendali", "tertib", "dukung",
        "untung", "sejahtera", "damai", "stabil", "optimis", "juara", "prestasi", "inovasi", "gratis",
        "murah", "cepat", "mudah", "lancar", "senang", "bangga", "solusi", "capai", "tercipta", "terbaik",
        // English
        "good", "great", "success", "successful", "improve", "improved", "growth", "record", "positive",
        "safe", "win", "benefit", "recovery", "strong", "promising", "innovative", "efficient", "bridging",
    };

    private static readonly HashSet<string> Negative = new(StringComparer.OrdinalIgnoreCase)
    {
        // Indonesian
        "buruk", "gagal", "turun", "melemah", "anjlok", "korban", "rugi", "kerugian", "bahaya", "ancaman",
        "terancam", "bocor", "kebocoran", "serangan", "lumpuh", "penipuan", "kejahatan", "kritik", "protes",
        "demo", "konflik", "krisis", "darurat", "banjir", "bencana", "melonjak", "memburuk", "keluhkan",
        "polemik", "penggusuran", "perundungan", "viral", "resah", "panik", "tolak", "kecewa", "marah",
        "mahal", "lambat", "macet", "rusak", "gangguan", "ditunda", "memanas", "sindir", "deadlock",
        // English
        "bad", "fail", "failure", "attack", "breach", "leak", "threat", "crisis", "fraud", "scam",
        "layoff", "layoffs", "decline", "deadlock", "urgent", "warning", "phishing", "ransomware", "affected",
    };

    private static readonly HashSet<string> Negations = new(StringComparer.OrdinalIgnoreCase)
    {
        "tidak", "bukan", "tanpa", "belum", "jangan", "kurang", "not", "no", "never", "without"
    };

    public static (double Score, string Label) Analyze(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return (0, "neutral");
        var words = text.Split(new[] { ' ', '\n', '\t', ',', '.', ';', ':', '!', '?', '(', ')', '"' },
            StringSplitOptions.RemoveEmptyEntries);
        double score = 0;
        var hits = 0;
        for (var i = 0; i < words.Length; i++)
        {
            var w = words[i].Trim('#', '@', '\'');
            double v = 0;
            if (Positive.Contains(w)) v = 1;
            else if (Negative.Contains(w)) v = -1;
            if (v == 0) continue;
            if (i > 0 && Negations.Contains(words[i - 1])) v = -v;
            score += v;
            hits++;
        }
        if (hits == 0) return (0, "neutral");
        var normalized = Math.Clamp(score / Math.Max(3, hits), -1, 1);
        var label = normalized > 0.15 ? "positive" : normalized < -0.15 ? "negative" : "neutral";
        return (Math.Round(normalized, 3), label);
    }
}

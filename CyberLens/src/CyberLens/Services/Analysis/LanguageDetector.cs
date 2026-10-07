namespace CyberLens.Services.Analysis;

/// <summary>
/// Fast heuristic language detection (Indonesian vs English) used when normalizing crawled
/// items. Cheap enough for high-volume streams: a title/summary is considered Indonesian
/// when it contains non-ASCII characters (typical of Indonesian diacritics/punctuation) or
/// common Indonesian function words. Powers the Dalam Negeri / Luar Negeri crawler mode.
/// </summary>
public static class LanguageDetector
{
    private static readonly HashSet<string> IndonesianMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        "yang", "di", "ke", "dari", "dan", "dengan", "untuk", "pada", "adalah", "tidak",
        "ini", "itu", "akan", "sudah", "telah", "juga", "atau", "para", "karena", "agar",
        "kepada", "oleh", "sebagai", "tersebut", "kebijakan", "pemerintah", "nasional",
        "pemberitaan", "berita", "jakarta", "indonesia", "bssn", "kominfo", "presiden",
        "bisa", "kami", "kita", "mereka", "saat", "dalam", "bukan", "hanya", "tentang",
        "antara", "serta", "hari", "tahun", "terkait", "mengatakan", "menurut", "warga",
        "dapat", "polisi", "daerah"
    };

    private static readonly HashSet<string> EnglishMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "of", "to", "in", "is", "that", "for", "it", "as", "was",
        "with", "on", "at", "by", "from", "this", "be", "are", "have", "has",
        "had", "an", "they", "which", "one", "you", "were", "her", "all", "their"
    };

    /// <summary>Returns "id" when the text looks Indonesian, otherwise "en".</summary>
    public static string Detect(string title, string summary)
    {
        var text = $"{title} {summary}";
        var words = text.Split(new[] { ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '(', ')', '"', '\'', '-', '/', '“', '”', '’' },
            StringSplitOptions.RemoveEmptyEntries);

        var idCount = 0;
        var enCount = 0;

        foreach (var w in words)
        {
            if (IndonesianMarkers.Contains(w)) idCount++;
            if (EnglishMarkers.Contains(w)) enCount++;
        }

        if (idCount > 0 && idCount >= enCount) return "id";
        if (enCount > idCount) return "en";

        return idCount > 0 ? "id" : (enCount > 0 ? "en" : "id");
    }
}

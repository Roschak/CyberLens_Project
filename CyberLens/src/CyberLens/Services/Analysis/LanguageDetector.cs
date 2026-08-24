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
        "pemberitaan", "berita", "jakarta", "indonesia", "bssn", "kominfo", "presiden"
    };

    /// <summary>Returns "id" when the text looks Indonesian, otherwise "en".</summary>
    public static string Detect(string title, string summary)
    {
        var text = $" {title} {summary} ";
        if (text.Any(c => c > 127)) return "id";
        foreach (var marker in IndonesianMarkers)
            if (text.Contains($" {marker} ")) return "id";
        return "en";
    }
}

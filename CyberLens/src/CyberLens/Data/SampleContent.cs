using System.Security.Cryptography;
using System.Text;

namespace CyberLens.Data;

public static class SampleContent
{
    public static string Sha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string ExtractTags(string text)
    {
        var words = text.ToLowerInvariant()
            .Split(new[] { ' ', '.', ',', '!', '?', ':', ';', '"', '\'', '(', ')', '[', ']', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 4 && !StopWords.Contains(w))
            .Distinct()
            .Take(6);

        return string.Join(",", words);
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "yang", "untuk", "dengan", "pada", "adalah", "dalam", "akan", "dari", "atau", "oleh",
        "juga", "hanya", "kami", "kita", "bisa", "harus", "sudah", "telah", "oleh", "serta",
        "satu", "dua", "tiga", "ini", "itu", "ada", "tidak", "bukan", "saat", "oleh", "secara",
        "agar", "bahkan", "bahwa", "sebagai", "sehingga", "mengenai", "terkait", "tersebut", "dapat"
    };

    public record LocationInfo(string Name, double Lat, double Lon);

    public static readonly LocationInfo[] Locations = new[]
    {
        new LocationInfo("Jakarta Pusat", -6.1805, 106.8284),
        new LocationInfo("Jakarta Selatan", -6.2615, 106.8106),
        new LocationInfo("IKN Nusantara, Kaltim", -0.9664, 116.7011),
        new LocationInfo("Surabaya, Jawa Timur", -7.2575, 112.7521),
        new LocationInfo("Bandung, Jawa Barat", -6.9175, 107.6191),
        new LocationInfo("Medan, Sumatera Utara", 3.5952, 98.6722),
        new LocationInfo("Makassar, Sulawesi Selatan", -5.1477, 119.4327),
        new LocationInfo("Semarang, Jawa Tengah", -6.9667, 110.4167),
        new LocationInfo("Yogyakarta, DIY", -7.7956, 110.3695),
        new LocationInfo("Palembang, Sumatera Selatan", -2.9909, 104.7565),
        new LocationInfo("Balikpapan, Kalimantan Timur", -1.2379, 116.8529),
        new LocationInfo("Denpasar, Bali", -8.6705, 115.2126),
        new LocationInfo("Jayapura, Papua", -2.5489, 140.7186),
        new LocationInfo("Batam, Kepulauan Riau", 1.1301, 104.0529),
        new LocationInfo("Manado, Sulawesi Utara", 1.4748, 124.8428),
    };
}

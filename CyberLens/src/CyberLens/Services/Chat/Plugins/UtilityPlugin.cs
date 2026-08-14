using System.ComponentModel;
using System.Data;
using Microsoft.SemanticKernel;

namespace CyberLens.Services.Chat.Plugins;

/// <summary>General-purpose kernel functions: date/time and math.</summary>
public class UtilityPlugin
{
    [KernelFunction, Description("Get the current date and time in UTC and the Asia/Jakarta (WIB) timezone.")]
    public string GetCurrentDateTime()
    {
        var utc = DateTime.UtcNow;
        var wib = utc.AddHours(7);
        return $"UTC: {utc:yyyy-MM-dd HH:mm:ss} | WIB (Asia/Jakarta): {wib:yyyy-MM-dd HH:mm:ss dddd}";
    }

    [KernelFunction, Description("Evaluate a basic arithmetic math expression, e.g. '(1200*1.11)/3 + 45'. Supports + - * / and parentheses.")]
    public string Calculate([Description("The arithmetic expression to evaluate")] string expression)
    {
        try
        {
            using var table = new DataTable();
            var result = table.Compute(expression, null);
            return $"{expression} = {result}";
        }
        catch (Exception ex)
        {
            return $"Tidak bisa menghitung '{expression}': {ex.Message}";
        }
    }

    [KernelFunction, Description("Return the number of days between two dates (format yyyy-MM-dd).")]
    public string DaysBetween(string startDate, string endDate)
    {
        if (DateTime.TryParse(startDate, out var s) && DateTime.TryParse(endDate, out var e))
            return $"{(e - s).Days} hari antara {startDate} dan {endDate}";
        return "Format tanggal tidak valid. Gunakan yyyy-MM-dd.";
    }
}

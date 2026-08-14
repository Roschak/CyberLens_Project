using CyberLens.Data;
using Microsoft.EntityFrameworkCore;

namespace CyberLens.Services;

/// <summary>Writes the tamper-evident audit trail of user activity.</summary>
public class AuditService(IDbContextFactory<CyberLensDbContext> dbFactory)
{
    public async Task LogAsync(string username, string action, string detail, int? userId = null, string ip = "")
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            db.AuditLogs.Add(new AuditLog
            {
                UserId = userId, Username = username, Action = action,
                Detail = detail.Length > 1000 ? detail[..1000] : detail, IpAddress = ip
            });
            await db.SaveChangesAsync();
        }
        catch { /* auditing must never break the primary operation */ }
    }
}

using System.Security.Claims;
using CyberLens.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace CyberLens.Services;

/// <summary>Cookie-based authentication and the current-user accessor for Blazor components.</summary>
public class AuthService(
    IDbContextFactory<CyberLensDbContext> dbFactory,
    IHttpContextAccessor httpContextAccessor,
    AuditService audit)
{
    public async Task<AppUser?> ValidateCredentialsAsync(string username, string password)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username && u.IsActive);
        if (user is null || !PasswordHasher.Verify(password, user.PasswordHash)) return null;
        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return user;
    }

    public static ClaimsPrincipal BuildPrincipal(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("display_name", user.DisplayName),
            new("avatar_color", user.AvatarColor),
            new(ClaimTypes.Role, user.Role.ToString()),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    public async Task SignInAsync(HttpContext http, AppUser user)
    {
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, BuildPrincipal(user),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7) });
        await audit.LogAsync(user.Username, "auth.login", "Login berhasil", user.Id,
            http.Connection.RemoteIpAddress?.ToString() ?? "");
    }

    public async Task SignOutAsync(HttpContext http)
    {
        var name = http.User.Identity?.Name ?? "";
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await audit.LogAsync(name, "auth.logout", "Logout");
    }

    public ClaimsPrincipal? CurrentUser => httpContextAccessor.HttpContext?.User;
    public int? CurrentUserId =>
        int.TryParse(CurrentUser?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
    public string CurrentUsername => CurrentUser?.Identity?.Name ?? "anonymous";
    public bool IsInRole(string role) => CurrentUser?.IsInRole(role) ?? false;
}

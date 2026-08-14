using System.Text.Json;
using CyberLens.Models;

namespace CyberLens.Services;

/// <summary>
/// Loads and persists the editable application configuration (config/cyberlens.settings.json).
/// Registered as a singleton; consumers read <see cref="Current"/> on every use so saved
/// changes apply immediately (database/storage provider changes require a restart).
/// </summary>
public class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
    private readonly string _path;
    private readonly object _gate = new();

    public AppConfig Current { get; private set; }
    public event Action? Changed;

    public AppSettingsService(IWebHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, "config");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "cyberlens.settings.json");
        Current = Load();
    }

    private AppConfig Load()
    {
        if (File.Exists(_path))
        {
            try
            {
                var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_path));
                if (cfg is not null) return cfg;
            }
            catch { /* fall through to defaults on corrupt file */ }
        }
        var fresh = new AppConfig();
        Save(fresh);
        return fresh;
    }

    public void Save(AppConfig config)
    {
        lock (_gate)
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(config, JsonOpts));
            Current = config;
        }
        Changed?.Invoke();
    }
}

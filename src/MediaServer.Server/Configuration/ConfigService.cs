using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MediaServer.Server.Configuration;

public sealed class ConfigService
{
    private readonly ILogger<ConfigService> _logger;
    private readonly string _configPath;
    private readonly FileSystemWatcher _watcher;
    private readonly object _gate = new();
    private AppConfig _current;

    public event EventHandler<AppConfig>? Changed;

    public ConfigService(ILogger<ConfigService> logger)
    {
        _logger = logger;
        _configPath = ResolveConfigPath();
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        _current = LoadConfig();
        _watcher = new FileSystemWatcher(Path.GetDirectoryName(_configPath)!, Path.GetFileName(_configPath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
        };
        _watcher.Changed += (_, _) => Reload();
        _watcher.Created += (_, _) => Reload();
        _watcher.Renamed += (_, _) => Reload();
        _watcher.EnableRaisingEvents = true;
    }

    public AppConfig Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    private static string ResolveConfigPath()
    {
        var basePath = AppPaths.GetDefaultDataRoot();
        var configDirectory = Path.Combine(basePath, "config");
        return Path.Combine(configDirectory, "config.json");
    }

    private AppConfig LoadConfig()
    {
        if (!File.Exists(_configPath))
        {
            var defaultConfig = new AppConfig();
            Persist(defaultConfig);
            return defaultConfig;
        }

        using var stream = File.OpenRead(_configPath);
        var config = JsonSerializer.Deserialize<AppConfig>(stream, JsonOptions.Options);
        return config ?? new AppConfig();
    }

    private void Persist(AppConfig config)
    {
        using var stream = File.Create(_configPath);
        JsonSerializer.Serialize(stream, config, JsonOptions.OptionsIndented);
    }

    private void Reload()
    {
        try
        {
            var config = LoadConfig();
            lock (_gate)
            {
                _current = config;
            }
            Changed?.Invoke(this, config);
            _logger.LogInformation("Configuration reloaded from {Path}", _configPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload configuration");
        }
    }

    public void Update(AppConfig config)
    {
        lock (_gate)
        {
            _current = config;
            Persist(config);
        }
        Changed?.Invoke(this, config);
    }
}

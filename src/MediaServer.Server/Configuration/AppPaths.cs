namespace MediaServer.Server.Configuration;

public sealed class AppPaths
{
    private readonly ConfigService _configService;

    public AppPaths(ConfigService configService)
    {
        _configService = configService;
        Directory.CreateDirectory(DatabaseDirectory);
        Directory.CreateDirectory(StreamRoot);
        Directory.CreateDirectory(ArtworkPath);
        Directory.CreateDirectory(MetadataPath);
    }

    public static string GetDefaultDataRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MediaServer");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(home))
        {
            home = Environment.GetEnvironmentVariable("HOME") ?? "/tmp";
        }

        return Path.Combine(home, "MediaServer");
    }

    public string DataRoot => ExpandPath(_configService.Current.DataRoot);

    public string DatabaseDirectory => Path.Combine(DataRoot, "db");

    public string DatabasePath => Path.Combine(DatabaseDirectory, "media.db");

    public string StreamRoot => Path.Combine(DataRoot, "streams");

    public string ArtworkPath => Path.Combine(DataRoot, "artwork");

    public string MetadataPath => Path.Combine(DataRoot, "metadata");

    public string LogsPath => Path.Combine(DataRoot, "logs");

    public string ConfigPath => Path.Combine(DataRoot, "config");

    private static string ExpandPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return GetDefaultDataRoot();
        }

        var expanded = Environment.ExpandEnvironmentVariables(path);
        return Path.GetFullPath(expanded);
    }
}

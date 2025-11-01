using System.Text.Json.Serialization;

namespace MediaServer.Server.Configuration;

public sealed class AppConfig
{
    public string DataRoot { get; set; } = AppPaths.GetDefaultDataRoot();

    public List<LibraryConfig> Libraries { get; set; } = new()
    {
        new LibraryConfig
        {
            Name = "Movies",
            Path = OperatingSystem.IsWindows() ? @"D:\\Media\\Movies" : "/srv/media/movies",
            Kind = "movies"
        },
        new LibraryConfig
        {
            Name = "Series",
            Path = OperatingSystem.IsWindows() ? @"D:\\Media\\Series" : "/srv/media/series",
            Kind = "series"
        }
    };

    public string FfmpegPath { get; set; } = OperatingSystem.IsWindows()
        ? @"C:\\MediaServer\\bin\\ffmpeg.exe"
        : "/usr/bin/ffmpeg";

    public string FfprobePath { get; set; } = OperatingSystem.IsWindows()
        ? @"C:\\MediaServer\\bin\\ffprobe.exe"
        : "/usr/bin/ffprobe";

    public List<string> AllowedNetworks { get; set; } = new()
    {
        "127.0.0.1/32",
        "10.0.0.0/8",
        "172.16.0.0/12",
        "192.168.0.0/16"
    };

    public bool AutoApproveLanDevices { get; set; } = true;

    public string? TmdbApiKey { get; set; }

    public bool DiscoveryEnabled { get; set; } = true;

    public int MetadataRefreshMinutes { get; set; } = 30;
}

public sealed class LibraryConfig
{
    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string Kind { get; set; } = "movies";

    [JsonIgnore]
    public string NormalizedPath => Path.Trim().TrimEnd(System.IO.Path.DirectorySeparatorChar);
}

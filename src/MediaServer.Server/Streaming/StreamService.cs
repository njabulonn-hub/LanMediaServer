using System.Collections.Concurrent;
using System.Diagnostics;
using MediaServer.Server.Configuration;
using MediaServer.Server.Data;
using Microsoft.Extensions.Logging;

namespace MediaServer.Server.Streaming;

public sealed class StreamService
{
    private readonly AppPaths _paths;
    private readonly ConfigService _configService;
    private readonly ILogger<StreamService> _logger;
    private readonly ConcurrentDictionary<Guid, StreamHandle> _streams = new();
    private readonly TimeSpan _ttl = TimeSpan.FromHours(6);

    public StreamService(AppPaths paths, ConfigService configService, ILogger<StreamService> logger)
    {
        _paths = paths;
        _configService = configService;
        _logger = logger;
    }

    public Task<string> CreateHlsStreamAsync(MediaItemDto item)
    {
        CleanupExpired();

        var ffmpeg = _configService.Current.FfmpegPath;
        if (!File.Exists(ffmpeg))
        {
            _logger.LogWarning("ffmpeg not found at {Path}; falling back to direct play", ffmpeg);
            return Task.FromResult($"/api/item/{item.Id}/file");
        }

        var streamId = Guid.NewGuid();
        var folder = Path.Combine(_paths.StreamRoot, streamId.ToString("N"));
        Directory.CreateDirectory(folder);
        var psi = new ProcessStartInfo
        {
            FileName = ffmpeg,
            Arguments = $"-y -i \"{item.FilePath}\" -codec:v libx264 -preset veryfast -b:v 4000k -codec:a aac -f hls -hls_time 4 -hls_list_size 0 index.m3u8",
            WorkingDirectory = folder,
            RedirectStandardError = true,
            RedirectStandardOutput = false,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            var process = Process.Start(psi);
            if (process is null)
            {
                _logger.LogWarning("Failed to start ffmpeg process");
                return Task.FromResult($"/api/item/{item.Id}/file");
            }

            _ = ConsumeErrorAsync(process);
            var handle = new StreamHandle(streamId, item.Id, folder, DateTimeOffset.UtcNow, process);
            _streams[streamId] = handle;
            return Task.FromResult($"/streams/{streamId:N}/index.m3u8");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ffmpeg invocation failed");
            return Task.FromResult($"/api/item/{item.Id}/file");
        }
    }

    private async Task ConsumeErrorAsync(Process process)
    {
        try
        {
            var error = await process.StandardError.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(error))
            {
                _logger.LogDebug("ffmpeg: {Error}", error);
            }
        }
        catch
        {
            // ignored
        }
    }

    private void CleanupExpired()
    {
        var threshold = DateTimeOffset.UtcNow - _ttl;
        foreach (var entry in _streams.ToArray())
        {
            if (entry.Value.CreatedAt < threshold)
            {
                if (_streams.TryRemove(entry.Key, out var handle))
                {
                    TryTerminate(handle.Process);
                    TryDeleteDirectory(handle.Directory);
                }
            }
        }
    }

    private static void TryTerminate(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch
        {
            // ignored
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
        catch
        {
            // best effort
        }
    }

    private sealed record StreamHandle(Guid Id, long ItemId, string Directory, DateTimeOffset CreatedAt, Process? Process);
}

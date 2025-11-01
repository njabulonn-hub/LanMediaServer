using MediaServer.Server.Data;
using Microsoft.Extensions.Logging;

namespace MediaServer.Server.Scanning;

public sealed class ScanService
{
    private static readonly string[] SupportedExtensions =
    {
        ".mp4", ".mkv", ".avi", ".mov", ".m4v", ".mp3", ".flac", ".aac", ".wav"
    };

    private readonly MediaRepository _repository;
    private readonly FfprobeService _ffprobe;
    private readonly ILogger<ScanService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool IsScanning { get; private set; }
    public DateTimeOffset? LastScanStarted { get; private set; }
    public DateTimeOffset? LastScanCompleted { get; private set; }

    public ScanService(MediaRepository repository, FfprobeService ffprobe, ILogger<ScanService> logger)
    {
        _repository = repository;
        _ffprobe = ffprobe;
        _logger = logger;
    }

    public Task QueueFullScanAsync()
    {
        _ = Task.Run(() => RunFullScanAsync(CancellationToken.None));
        return Task.CompletedTask;
    }

    public async Task RunFullScanAsync(CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            _logger.LogInformation("Scan already in progress");
            return;
        }

        try
        {
            IsScanning = true;
            LastScanStarted = DateTimeOffset.UtcNow;
            var libraries = await _repository.GetLibrariesAsync();
            foreach (var library in libraries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ScanLibraryAsync(library, cancellationToken);
            }
        }
        finally
        {
            IsScanning = false;
            LastScanCompleted = DateTimeOffset.UtcNow;
            _gate.Release();
        }
    }

    private async Task ScanLibraryAsync(LibraryDto library, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(library.Path))
        {
            _logger.LogWarning("Library path {Path} is missing", library.Path);
            return;
        }

        var files = Directory.EnumerateFiles(library.Path, "*", SearchOption.AllDirectories)
            .Where(file => SupportedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .ToList();

        _logger.LogInformation("Scanning {Count} files in {Library}", files.Count, library.Name);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessFileAsync(library.Id, file, cancellationToken);
        }
    }

    private async Task ProcessFileAsync(long libraryId, string filePath, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var title = Path.GetFileNameWithoutExtension(filePath);
        var mediaId = await _repository.UpsertMediaItemAsync(libraryId, filePath, title, extension);

        if (_ffprobe.IsConfigured)
        {
            var info = await _ffprobe.ProbeAsync(filePath, cancellationToken);
            if (info is not null)
            {
                await _repository.UpdateMediaMetadataAsync(mediaId, info);
            }
        }
    }
}

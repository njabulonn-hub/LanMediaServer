using System.Diagnostics;
using System.Text.Json;
using MediaServer.Server.Configuration;
using Microsoft.Extensions.Logging;

namespace MediaServer.Server.Scanning;

public sealed class FfprobeService
{
    private readonly ConfigService _configService;
    private readonly ILogger<FfprobeService> _logger;

    public FfprobeService(ConfigService configService, ILogger<FfprobeService> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    public bool IsConfigured => File.Exists(_configService.Current.FfprobePath);

    public async Task<MediaTechnicalInfo?> ProbeAsync(string filePath, CancellationToken cancellationToken)
    {
        var config = _configService.Current;
        if (!File.Exists(config.FfprobePath))
        {
            _logger.LogDebug("ffprobe not found at {Path}", config.FfprobePath);
            return null;
        }

        var psi = new ProcessStartInfo
        {
            FileName = config.FfprobePath,
            Arguments = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                _logger.LogWarning("Failed to start ffprobe process");
                return null;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("ffprobe exited with {Code}: {Error}", process.ExitCode, error);
                return null;
            }

            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;
            double? duration = null;
            string? videoCodec = null;
            string? audioCodec = null;

            if (root.TryGetProperty("format", out var format) &&
                format.TryGetProperty("duration", out var durationProperty) &&
                double.TryParse(durationProperty.GetString(), out var durationValue))
            {
                duration = durationValue;
            }

            if (root.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
            {
                foreach (var stream in streams.EnumerateArray())
                {
                    if (!stream.TryGetProperty("codec_type", out var codecType))
                    {
                        continue;
                    }

                    var type = codecType.GetString();
                    if (type == "video" && videoCodec is null && stream.TryGetProperty("codec_name", out var codecName))
                    {
                        videoCodec = codecName.GetString();
                    }
                    else if (type == "audio" && audioCodec is null && stream.TryGetProperty("codec_name", out var audioName))
                    {
                        audioCodec = audioName.GetString();
                    }
                }
            }

            return new MediaTechnicalInfo(duration, videoCodec, audioCodec);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running ffprobe");
            return null;
        }
    }
}

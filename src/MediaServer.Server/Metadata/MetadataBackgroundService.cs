using MediaServer.Server.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediaServer.Server.Metadata;

public sealed class MetadataBackgroundService : BackgroundService
{
    private readonly MetadataWorker _worker;
    private readonly ConfigService _config;
    private readonly ILogger<MetadataBackgroundService> _logger;

    public MetadataBackgroundService(MetadataWorker worker, ConfigService config, ILogger<MetadataBackgroundService> logger)
    {
        _worker = worker;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await _worker.RunOnceAsync(stoppingToken);
                if (processed > 0)
                {
                    _logger.LogInformation("Enriched metadata for {Count} items", processed);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Metadata worker failed");
            }

            var minutes = Math.Max(1, _config.Current.MetadataRefreshMinutes);
            await Task.Delay(TimeSpan.FromMinutes(minutes), stoppingToken);
        }
    }
}

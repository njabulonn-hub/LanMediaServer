using MediaServer.Server.Configuration;
using MediaServer.Server.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediaServer.Server.Hosting;

public sealed class StartupInitializer : IHostedService
{
    private readonly ConfigService _configService;
    private readonly DatabaseInitializer _databaseInitializer;
    private readonly ILogger<StartupInitializer> _logger;

    public StartupInitializer(ConfigService configService, DatabaseInitializer databaseInitializer, ILogger<StartupInitializer> logger)
    {
        _configService = configService;
        _databaseInitializer = databaseInitializer;
        _logger = logger;
        _configService.Changed += async (_, config) =>
        {
            try
            {
                await _databaseInitializer.InitializeAsync(config, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to synchronize libraries after config reload");
            }
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Preparing database at startup");
        await _databaseInitializer.InitializeAsync(_configService.Current, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

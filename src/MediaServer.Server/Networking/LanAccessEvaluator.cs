using MediaServer.Server.Configuration;
using Microsoft.Extensions.Logging;

namespace MediaServer.Server.Networking;

public sealed class LanAccessEvaluator
{
    private readonly ConfigService _config;
    private readonly ILogger<LanAccessEvaluator> _logger;
    private volatile IReadOnlyList<IpNetwork> _networks;

    public LanAccessEvaluator(ConfigService config, ILogger<LanAccessEvaluator> logger)
    {
        _config = config;
        _logger = logger;
        _networks = BuildNetworks(config.Current.AllowedNetworks);
        _config.Changed += (_, appConfig) => _networks = BuildNetworks(appConfig.AllowedNetworks);
    }

    public bool IsAllowed(IPAddress? address)
    {
        if (address is null)
        {
            return false;
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        foreach (var network in _networks)
        {
            if (network.Contains(address))
            {
                return true;
            }
        }

        return false;
    }

    private IReadOnlyList<IpNetwork> BuildNetworks(IEnumerable<string> raw)
    {
        var result = new List<IpNetwork>();
        foreach (var entry in raw)
        {
            if (IpNetwork.TryParse(entry, out var network) && network is not null)
            {
                result.Add(network);
            }
            else
            {
                _logger.LogWarning("Invalid CIDR entry {Entry} ignored", entry);
            }
        }

        return result;
    }
}

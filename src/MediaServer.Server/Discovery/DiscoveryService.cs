using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using MediaServer.Server.Configuration;
using Microsoft.Extensions.Logging;

namespace MediaServer.Server.Discovery;

public sealed class DiscoveryService
{
    private readonly AppPaths _paths;
    private readonly ConfigService _config;
    private readonly ILogger<DiscoveryService> _logger;
    private readonly object _gate = new();
    private string _serviceName = "Local Media Server";
    private int _port = 8090;
    private readonly string _uuid;

    public DiscoveryService(AppPaths paths, ConfigService config, ILogger<DiscoveryService> logger)
    {
        _paths = paths;
        _config = config;
        _logger = logger;
        _uuid = LoadOrCreateUuid();
    }

    public void Configure(string serviceName, int port)
    {
        lock (_gate)
        {
            _serviceName = serviceName;
            _port = port;
        }
    }

    public DiscoverySettings GetSettings()
    {
        lock (_gate)
        {
            return new DiscoverySettings(_config.Current.DiscoveryEnabled, _serviceName, _port, _uuid, GetHostName(), GetLanAddresses());
        }
    }

    private string LoadOrCreateUuid()
    {
        var file = Path.Combine(_paths.ConfigPath, "discovery.id");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        if (File.Exists(file))
        {
            return File.ReadAllText(file).Trim();
        }

        var value = Guid.NewGuid().ToString();
        File.WriteAllText(file, value);
        return value;
    }

    private static string GetHostName()
    {
        var host = Dns.GetHostName();
        return host.Contains('.') ? host.Split('.')[0] : host;
    }

    private static IReadOnlyList<IPAddress> GetLanAddresses()
    {
        var addresses = new List<IPAddress>();
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            var ipProps = networkInterface.GetIPProperties();
            foreach (var unicast in ipProps.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    addresses.Add(unicast.Address);
                }
            }
        }

        if (addresses.Count == 0)
        {
            addresses.Add(IPAddress.Loopback);
        }

        return addresses;
    }
}

public sealed record DiscoverySettings(bool Enabled, string ServiceName, int Port, string Uuid, string HostName, IReadOnlyList<IPAddress> Addresses);

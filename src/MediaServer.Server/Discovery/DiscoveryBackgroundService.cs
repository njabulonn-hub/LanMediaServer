using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediaServer.Server.Discovery;

public sealed class DiscoveryBackgroundService : BackgroundService
{
    private static readonly IPEndPoint SsdpEndpoint = new(IPAddress.Parse("239.255.255.250"), 1900);
    private static readonly IPEndPoint MdnsEndpoint = new(IPAddress.Parse("224.0.0.251"), 5353);

    private readonly DiscoveryService _service;
    private readonly ILogger<DiscoveryBackgroundService> _logger;

    public DiscoveryBackgroundService(DiscoveryService service, ILogger<DiscoveryBackgroundService> logger)
    {
        _service = service;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var settings = _service.GetSettings();
                if (settings.Enabled)
                {
                    await BroadcastSsdpAsync(settings, stoppingToken);
                    await BroadcastMdnsAsync(settings, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Discovery broadcast failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task BroadcastSsdpAsync(DiscoverySettings settings, CancellationToken token)
    {
        foreach (var address in settings.Addresses)
        {
            var location = $"http://{address}:{settings.Port}/api/status";
            var payload =
                $"NOTIFY * HTTP/1.1\r\n" +
                $"HOST: 239.255.255.250:1900\r\n" +
                $"NT: urn:schemas-upnp-org:device:MediaServer:1\r\n" +
                $"NTS: ssdp:alive\r\n" +
                $"USN: uuid:{settings.Uuid}\r\n" +
                $"LOCATION: {location}\r\n" +
                $"CACHE-CONTROL: max-age=300\r\n" +
                $"SERVER: MediaServer/1.0 UPnP/1.0\r\n\r\n";

            using var client = new UdpClient(AddressFamily.InterNetwork);
            client.MulticastLoopback = false;
            var bytes = Encoding.UTF8.GetBytes(payload);
            await client.SendAsync(bytes, bytes.Length, SsdpEndpoint);
        }
    }

    private async Task BroadcastMdnsAsync(DiscoverySettings settings, CancellationToken token)
    {
        foreach (var address in settings.Addresses)
        {
            var message = BuildMdnsMessage(settings, address);
            using var client = new UdpClient(AddressFamily.InterNetwork);
            client.MulticastLoopback = false;
            client.JoinMulticastGroup(MdnsEndpoint.Address);
            await client.SendAsync(message, message.Length, MdnsEndpoint);
        }
    }

    private static byte[] BuildMdnsMessage(DiscoverySettings settings, IPAddress address)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write((ushort)0); // ID
        writer.Write((ushort)0x8400); // flags
        writer.Write((ushort)0); // questions
        writer.Write((ushort)3); // answers (PTR, SRV, TXT)
        writer.Write((ushort)0); // authority
        writer.Write((ushort)1); // additional (A record)

        var instanceName = settings.ServiceName.Replace(' ', '-');
        var serviceLabels = new[] { "_lanmedia", "_tcp", "local" };
        var instanceLabels = new[] { instanceName, "_lanmedia", "_tcp", "local" };
        var hostLabels = new[] { settings.HostName, "local" };

        WriteName(writer, serviceLabels);
        writer.Write((ushort)12); // PTR
        writer.Write((ushort)1); // class IN
        writer.Write((uint)120);
        using (var data = new MemoryStream())
        using (var dataWriter = new BinaryWriter(data, Encoding.UTF8, leaveOpen: true))
        {
            WriteName(dataWriter, instanceLabels);
            var buffer = data.ToArray();
            writer.Write((ushort)buffer.Length);
            writer.Write(buffer);
        }

        WriteName(writer, instanceLabels);
        writer.Write((ushort)33); // SRV
        writer.Write((ushort)1);
        writer.Write((uint)120);
        using (var data = new MemoryStream())
        using (var dataWriter = new BinaryWriter(data, Encoding.UTF8, leaveOpen: true))
        {
            dataWriter.Write((ushort)0); // priority
            dataWriter.Write((ushort)0); // weight
            dataWriter.Write((ushort)settings.Port);
            WriteName(dataWriter, hostLabels);
            var buffer = data.ToArray();
            writer.Write((ushort)buffer.Length);
            writer.Write(buffer);
        }

        WriteName(writer, instanceLabels);
        writer.Write((ushort)16); // TXT
        writer.Write((ushort)1);
        writer.Write((uint)120);
        writer.Write((ushort)1);
        writer.Write((byte)0); // empty txt

        WriteName(writer, hostLabels);
        writer.Write((ushort)1); // A
        writer.Write((ushort)1);
        writer.Write((uint)120);
        var addressBytes = address.GetAddressBytes();
        writer.Write((ushort)addressBytes.Length);
        writer.Write(addressBytes);

        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteName(BinaryWriter writer, IEnumerable<string> labels)
    {
        foreach (var label in labels)
        {
            var bytes = Encoding.UTF8.GetBytes(label);
            writer.Write((byte)bytes.Length);
            writer.Write(bytes);
        }

        writer.Write((byte)0);
    }
}

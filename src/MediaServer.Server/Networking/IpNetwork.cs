using System.Buffers.Binary;
using System.Net;

namespace MediaServer.Server.Networking;

public sealed class IpNetwork
{
    public IPAddress Address { get; }
    public int PrefixLength { get; }
    private readonly uint _mask;

    private IpNetwork(IPAddress address, int prefixLength, uint mask)
    {
        Address = address;
        PrefixLength = prefixLength;
        _mask = mask;
    }

    public static bool TryParse(string value, out IpNetwork? network)
    {
        network = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        if (!IPAddress.TryParse(parts[0], out var address))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var prefix) || prefix < 0 || prefix > 32)
        {
            return false;
        }

        var mask = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
        network = new IpNetwork(address.MapToIPv4(), prefix, mask);
        return true;
    }

    public bool Contains(IPAddress address)
    {
        var ipv4 = address.MapToIPv4();
        var addressBytes = ipv4.GetAddressBytes();
        var baseBytes = Address.GetAddressBytes();
        if (addressBytes.Length != 4 || baseBytes.Length != 4)
        {
            return false;
        }

        var addressValue = BinaryPrimitives.ReadUInt32BigEndian(addressBytes);
        var baseValue = BinaryPrimitives.ReadUInt32BigEndian(baseBytes);
        return (addressValue & _mask) == (baseValue & _mask);
    }
}

using System.Net;
using System.Net.Sockets;

namespace Lernkicker.Calendar;

/// <summary>
/// Keeps calendar imports from reaching into the home network or the tailnet (SSRF): only public addresses may be
/// contacted. Checked on every connection, so a redirect or a DNS answer pointing inwards fails too.
/// </summary>
public static class AddressGuard
{
    private static readonly (IPAddress Network, int Bits)[] BlockedV4 =
    [
        (IPAddress.Parse("0.0.0.0"), 8),
        (IPAddress.Parse("10.0.0.0"), 8),
        (IPAddress.Parse("100.64.0.0"), 10),   // carrier-grade NAT, also Tailscale's 100.x addresses
        (IPAddress.Parse("127.0.0.0"), 8),
        (IPAddress.Parse("169.254.0.0"), 16),
        (IPAddress.Parse("172.16.0.0"), 12),
        (IPAddress.Parse("192.0.0.0"), 24),
        (IPAddress.Parse("192.168.0.0"), 16),
        (IPAddress.Parse("198.18.0.0"), 15),
        (IPAddress.Parse("224.0.0.0"), 4),     // multicast
        (IPAddress.Parse("240.0.0.0"), 4),     // reserved and broadcast
    ];

    private static readonly (IPAddress Network, int Bits)[] BlockedV6 =
    [
        (IPAddress.Parse("fc00::"), 7),        // unique local, also Tailscale's fd7a:115c:a1e0::/48
        (IPAddress.Parse("fe80::"), 10),       // link-local
        (IPAddress.Parse("fec0::"), 10),       // site-local (deprecated)
        (IPAddress.Parse("ff00::"), 8),        // multicast
        (IPAddress.Parse("64:ff9b::"), 96),    // NAT64 can map to private IPv4
    ];

    public static bool IsAllowed(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.IPv6None))
        {
            return false;
        }

        var blocked = address.AddressFamily == AddressFamily.InterNetwork ? BlockedV4 : BlockedV6;
        return !blocked.Any(range => InRange(address, range.Network, range.Bits));
    }

    /// <summary>Connects to the first allowed address of the host, or throws <see cref="AddressNotAllowedException"/>.</summary>
    public static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host, out var literal) ? [literal] : await Dns.GetHostAddressesAsync(host, cancellationToken);
        var allowed = addresses.Where(IsAllowed).ToArray();
        if (allowed.Length == 0)
        {
            throw new AddressNotAllowedException(host);
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(allowed, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static bool InRange(IPAddress address, IPAddress network, int bits)
    {
        if (address.AddressFamily != network.AddressFamily)
        {
            return false;
        }

        var a = address.GetAddressBytes();
        var n = network.GetAddressBytes();
        var whole = bits / 8;
        for (var i = 0; i < whole; i++)
        {
            if (a[i] != n[i])
            {
                return false;
            }
        }

        var rest = bits % 8;
        if (rest == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - rest));
        return (a[whole] & mask) == (n[whole] & mask);
    }
}

public sealed class AddressNotAllowedException(string host) : Exception($"{host} is not a public address.");

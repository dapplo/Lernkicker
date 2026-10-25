using System.Net;

namespace Lernkicker.Calendar.Tests;

public class AddressGuardTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.178.20")] // the NAS at home
    [InlineData("100.101.102.103")] // a tailnet address
    [InlineData("169.254.169.254")] // cloud metadata
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    [InlineData("fd7a:115c:a1e0::1")] // Tailscale IPv6
    [InlineData("ff02::1")]
    [InlineData("::ffff:192.168.1.1")]
    [InlineData("64:ff9b::a00:1")]
    public void Private_addresses_are_refused(string address) =>
        Assert.False(AddressGuard.IsAllowed(IPAddress.Parse(address)));

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("172.32.0.1")]
    [InlineData("100.128.0.1")]
    [InlineData("193.99.144.80")]
    [InlineData("2a00:1450:4001:82a::200e")]
    [InlineData("::ffff:8.8.8.8")]
    public void Public_addresses_are_allowed(string address) =>
        Assert.True(AddressGuard.IsAllowed(IPAddress.Parse(address)));
}

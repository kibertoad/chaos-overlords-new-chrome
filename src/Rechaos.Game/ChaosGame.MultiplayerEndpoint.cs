using System.Net;
using System.Net.Sockets;
using Rechaos.Multiplayer.Http;

namespace Rechaos.Game;

/// <summary>
/// Which server the Online screen is pointed at, whether it is answering, and whether talking to it
/// would put the seat token and the lobby password on the wire in clear.
/// </summary>
public sealed partial class ChaosGame
{
    private bool TrySelectedServer(out Uri baseAddress)
    {
        if (_online.Service == OnlineServiceMode.Central)
        {
            baseAddress = MultiplayerServiceEndpoint.Central;
            return true;
        }
        var address = _online.Server.Value.Trim();
        return Uri.TryCreate(address, UriKind.Absolute, out baseAddress!)
            && baseAddress.Scheme is "http" or "https";
    }

    /// <summary>Checks the selected server without ever blocking the drawing thread.</summary>
    private void BeginServerProbe()
    {
        _serverProbeCancellation?.Cancel();
        _serverProbeCancellation?.Dispose();
        _serverProbeCancellation = null;
        _serverProbe = null;
        if (!TrySelectedServer(out var address))
        {
            _online.ServerStatus = "CUSTOM SERVER ADDRESS IS INVALID";
            return;
        }
        _online.ServerStatus = _online.Service == OnlineServiceMode.Central
            ? "CHECKING CENTRAL SERVER..."
            : "CHECKING CUSTOM SERVER...";
        _serverProbeCancellation = new CancellationTokenSource();
        _serverProbe = MultiplayerServiceEndpoint.IsHealthyAsync(
            _http, address, _serverProbeCancellation.Token);
    }

    /// <summary>Picks up the health result on the game thread.</summary>
    private void PumpServerProbe()
    {
        if (_serverProbe is not { IsCompleted: true } probe) return;
        _serverProbe = null;
        _serverProbeCancellation?.Dispose();
        _serverProbeCancellation = null;
        var healthy = probe.IsCompletedSuccessfully && probe.Result;
        var name = _online.Service == OnlineServiceMode.Central ? "CENTRAL" : "CUSTOM";
        if (!healthy)
        {
            _online.ServerStatus = $"{name} SERVER UNAVAILABLE";
            return;
        }
        // The line reads Lime only when it ends in ONLINE, so a server this player reaches in clear
        // gets the amber one. That is the point: nothing else in the interface says that the seat
        // token and the lobby password are about to cross a network readable.
        _online.ServerStatus = TrySelectedServer(out var address) && SendsCredentialsInClear(address)
            ? $"{name} SERVER ONLINE, NOT ENCRYPTED"
            : $"{name} SERVER ONLINE";
    }

    /// <summary>
    /// Whether this address would carry the membership token and the lobby password in clear.
    /// </summary>
    /// <remarks>
    /// <c>http</c> to another machine does. Loopback does not, and neither does a literal address in
    /// one of the private ranges or an mDNS <c>.local</c> name, which are the shapes a player running
    /// a server for the people in the room actually types. A hostname is warned about even when it
    /// happens to be a machine down the hall, because nothing here can tell that from a name that
    /// resolves across the internet.
    /// </remarks>
    private static bool SendsCredentialsInClear(Uri address)
    {
        if (!string.Equals(address.Scheme, "http", StringComparison.Ordinal)) return false;
        if (address.IsLoopback) return false;
        if (address.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)) return false;
        return !IPAddress.TryParse(address.Host, out var ip) || !IsPrivateAddress(ip);
    }

    private static bool IsPrivateAddress(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // fc00::/7 is the v6 equivalent of the v4 private ranges. The deprecated fec0::/10
            // site-local block is left out: nothing hands one out any more.
            return ip.IsIPv6LinkLocal || (ip.GetAddressBytes()[0] & 0xFE) == 0xFC;
        }
        var octets = ip.GetAddressBytes();
        return octets[0] == 10
            || (octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31)
            || (octets[0] == 192 && octets[1] == 168)
            || (octets[0] == 169 && octets[1] == 254);
    }
}

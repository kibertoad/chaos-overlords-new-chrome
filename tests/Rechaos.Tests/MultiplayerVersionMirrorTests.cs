using System.Text.RegularExpressions;
using Rechaos.Multiplayer.Protocol;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The two version constants, held equal to the TypeScript contracts that are their source.
/// </summary>
/// <remarks>
/// <para>
/// AGENTS.md makes both pairs a hard rule, and until this test nothing enforced either. The
/// protocol pair was covered indirectly, by the OnlineSmoke job running the real C# client against
/// the Node server — but only when a path filter matched, and never in the fast gate or in a
/// release. The session pair had no coverage at all: the server stores whatever
/// <c>sessionVersion</c> a client sends and never compares it to its own, so OnlineSmoke passes
/// with the two numbers different, and the published <c>contracts</c> and <c>client</c> packages
/// would ship disagreeing with the game about which stored matches are resumable.
/// </para>
/// <para>
/// The TypeScript file is linked into the test output by <c>Rechaos.Tests.csproj</c>, so this runs
/// where AGENTS.md sends every contributor: the fast validation gate.
/// </para>
/// </remarks>
public sealed class MultiplayerVersionMirrorTests
{
    [Fact]
    public void ProtocolVersionMatchesTheTypeScriptContract()
    {
        Assert.Equal(
            MultiplayerProtocolVersion.Current,
            ReadConstant("MULTIPLAYER_PROTOCOL_VERSION"));
    }

    [Fact]
    public void SessionVersionMatchesTheTypeScriptContract()
    {
        Assert.Equal(
            MultiplayerSessionVersion.Current,
            ReadConstant("MULTIPLAYER_SESSION_VERSION"));
    }

    private static int ReadConstant(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "contracts-protocol.ts");
        Assert.True(File.Exists(path), $"The contracts protocol source is missing at {path}.");
        var source = File.ReadAllText(path);
        var match = Regex.Match(
            source,
            $@"export\s+const\s+{Regex.Escape(name)}\s*=\s*(?<value>-?\d+)",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));
        Assert.True(match.Success, $"{name} was not found in the contracts protocol source.");
        return int.Parse(match.Groups["value"].Value, System.Globalization.CultureInfo.InvariantCulture);
    }
}

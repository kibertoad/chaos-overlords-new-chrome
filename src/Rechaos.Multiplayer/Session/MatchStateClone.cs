using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// A deep copy of a match, through the same serializer a quick-save uses.
/// </summary>
/// <remarks>
/// <para>
/// Online play keeps two states. The authoritative one advances only by applying sealed turns, and
/// the copy is what the player plans on: queueing a command has to show up in the interface at once,
/// but applying it locally before the turn seals would put this client's state ahead of everyone
/// else's, and the sealed set would then apply it a second time.
/// </para>
/// <para>
/// Round-tripping the native format is the copy: it is the same code path the game already trusts
/// for a save and a snapshot upload, so a field it does not carry is a field no client would have
/// recovered either — a bug that would surface identically on a reload.
/// </para>
/// </remarks>
public static class MatchStateClone
{
    /// <summary>An independent copy of <paramref name="state"/>.</summary>
    public static MatchState Of(MatchState state, OriginalData definitions)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(definitions);
        using var stream = new MemoryStream();
        NativeSaveSerializer.Save(stream, state);
        stream.Position = 0;
        return NativeSaveSerializer.Load(stream, definitions);
    }

    /// <summary>The native snapshot bytes a desync repair uploads, base64-encoded.</summary>
    public static string ToBase64(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        using var stream = new MemoryStream();
        NativeSaveSerializer.Save(stream, state);
        return Convert.ToBase64String(stream.ToArray());
    }

    /// <summary>The state inside a snapshot body the server relayed.</summary>
    public static MatchState FromBase64(string body, OriginalData definitions)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(definitions);
        using var stream = new MemoryStream(Convert.FromBase64String(body), writable: false);
        return NativeSaveSerializer.Load(stream, definitions);
    }
}

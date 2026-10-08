using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Game;

/// <summary>One seat a player can still return to, as the interface reads it.</summary>
/// <remarks>
/// <para>
/// Two of the names here are easy to confuse. <c>DisplayName</c> is the player's own name in that
/// match; <c>SessionName</c> is the match's own, which is what a list of sessions has to be read by.
/// The match's name reaches every member on the wire, so it is kept for every member rather than
/// only for the host who typed it.
/// </para>
/// <para>
/// <c>LastUpdatedAt</c> is when this seat last had turn data stored against it, which is what tells
/// two unfinished sessions apart when both are still resumable. It is null, and <c>SessionName</c>
/// empty, in a record written by a build that stored neither.
/// </para>
/// <para>
/// <c>ComlinkKey</c> is the seat's Comlink private key (PKCS#8, base64), the only thing that opens
/// the messages other seats sealed to it. It is empty in a record from a build that kept none, and
/// a resume then publishes a fresh key.
/// </para>
/// </remarks>
public sealed record MultiplayerRecovery(
    int FormatVersion,
    string Server,
    string MatchId,
    string PlayerId,
    string Token,
    string JoinCode,
    string DisplayName,
    bool IsHost,
    bool CleanExit,
    bool Completed,
    string Password = "",
    int SessionVersion = MultiplayerSessionVersion.Initial,
    string SessionName = "",
    DateTimeOffset? LastUpdatedAt = null,
    MultiplayerRecoveryFailure? LastFailure = null,
    string ComlinkKey = "")
{
    public const int CurrentFormatVersion = 1;

    /// <summary>Whether this build plays the session this seat belongs to.</summary>
    /// <remarks>
    /// The membership stays worth keeping either way — the seat is still held, and a build of that
    /// session version can take it — so this is asked beside <see cref="CanReconnect"/> rather than
    /// folded into it. The browser of this build lists only what it can resume.
    /// </remarks>
    public bool IsCompatible => MultiplayerSessionVersion.CanResume(SessionVersion);

    public bool ShouldSuggestReconnect => !CleanExit && !Completed && IsCompatible;
    public bool CanReconnect => !Completed;

    /// <summary>The seat is still live and this build can carry the match on.</summary>
    public bool CanResume => CanReconnect && IsCompatible;
}

/// <summary>
/// The last non-recoverable online failure for a saved membership.
/// </summary>
/// <remarks>
/// This is a deliberately small forensic breadcrumb, not a replay or a transport capture. It
/// carries only machine-readable protocol context that can be safely included in a diagnostics
/// export; credentials, player names, match settings and orders never belong here.
/// </remarks>
public sealed record MultiplayerRecoveryFailure(
    DateTimeOffset OccurredAt,
    string Stage,
    string? Operation,
    int? HttpStatus,
    string? Reason,
    string? RequestId,
    int? PlanningTurn,
    int? LastEventSequence);

/// <summary>
/// One membership as it sits on disk.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="MultiplayerRecovery"/> because the token is not stored the way it is
/// held. <see cref="ProtectedToken"/> carries it sealed with DPAPI to the current user account on
/// Windows. <see cref="TokenStore"/> names the operating-system store that holds it on macOS
/// (<c>keychain</c>) and Linux (<c>secret-service</c>), and <see cref="TokenAccount"/> the name it
/// is filed under there; see <see cref="RecoveryTokenProtection"/>. <see cref="Token"/> carries it in clear where
/// neither is available. Exactly one of the three is set. A file written by a build that predates
/// the sealed forms has only <see cref="Token"/>, which is why reading that is still supported, and
/// why the first load that can seal it rewrites the file without it.
/// </para>
/// <para>
/// <see cref="Password"/> is stored in the clear, unlike the token. It opens one session's door to
/// anyone the player was going to read it out to anyway, where the token is that seat itself; and
/// the reason to keep it is that the player who resumes has to be able to read it out again.
/// A file from a build that did not write it has none, which reads back as a session without one.
/// </para>
/// <para>
/// <see cref="SessionVersion"/> is kept so the browser can leave out a seat this build cannot take
/// before the game dials the server for it. It is additive in both directions, which is why it does not
/// move <see cref="MultiplayerRecoveryHistory.CurrentFormatVersion"/>: a build that does not know
/// the field ignores it and keeps its reconnects, and a build that does reads a file without one
/// as <see cref="MultiplayerSessionVersion.Initial"/>, the only version that can have been stored
/// before the field existed. The file is a hint either way — the match view settles it.
/// </para>
/// </remarks>
internal sealed record PersistedRecovery(
    int FormatVersion,
    string Server,
    string MatchId,
    string PlayerId,
    string JoinCode,
    string DisplayName,
    bool IsHost,
    bool CleanExit,
    bool Completed,
    string? Token = null,
    string? ProtectedToken = null,
    string? Password = null,
    int? SessionVersion = null,
    string? SessionName = null,
    DateTimeOffset? LastUpdatedAt = null,
    MultiplayerRecoveryFailure? LastFailure = null,
    string? TokenStore = null,
    string? TokenAccount = null,
    string? ComlinkKey = null,
    string? ProtectedComlinkKey = null);

internal sealed record MultiplayerRecoveryHistory(
    int FormatVersion,
    IReadOnlyList<PersistedRecovery> Sessions)
{
    /// <summary>
    /// Version 7 added <see cref="PersistedRecovery.ComlinkKey"/> and
    /// <see cref="PersistedRecovery.ProtectedComlinkKey"/>, sealed with DPAPI on Windows; 6 added
    /// <see cref="PersistedRecovery.TokenStore"/> and
    /// <see cref="PersistedRecovery.TokenAccount"/>; 5 added
    /// <see cref="PersistedRecovery.LastFailure"/>; 4 added <see cref="PersistedRecovery.SessionName"/> and
    /// <see cref="PersistedRecovery.LastUpdatedAt"/>; 3 added
    /// <see cref="PersistedRecovery.ProtectedToken"/>; 2 is still read. Every field these versions
    /// added is optional, so an older file reads back as a membership that simply knows less about
    /// itself rather than one that cannot be resumed.
    /// </summary>
    internal const int CurrentFormatVersion = 7;
    internal const int OldestReadableFormatVersion = 2;

    /// <summary>
    /// The newest version without <see cref="PersistedRecovery.TokenStore"/>, which a file is still
    /// written as when no membership in it uses that field.
    /// </summary>
    /// <remarks>
    /// A build of version 5 would read a token kept in an operating-system store as a membership
    /// without a token, drop it, and on its next save write the file without it: the seat would be
    /// gone. Stamping such a file 6 makes that build leave it alone instead. A file with no such
    /// membership, which is every file on Windows, stays readable by that build, so going back one
    /// build there costs nothing.
    /// </remarks>
    internal const int FormatVersionWithoutTokenStore = 5;

    /// <summary>
    /// The newest version without <see cref="PersistedRecovery.ComlinkKey"/> and
    /// <see cref="PersistedRecovery.ProtectedComlinkKey"/>.
    /// </summary>
    /// <remarks>
    /// A build of version 6 would read the file, drop the Comlink keys it does not know, and write
    /// the file back without them, so the seat would lose every message sealed to it. A file that
    /// holds a key is stamped 7, which that build leaves alone.
    /// </remarks>
    internal const int FormatVersionWithoutComlinkKey = 6;

    internal static int FormatVersionFor(IEnumerable<PersistedRecovery> sessions)
    {
        var all = sessions as IReadOnlyCollection<PersistedRecovery> ?? sessions.ToArray();
        if (all.Any(session => session.ComlinkKey is not null || session.ProtectedComlinkKey is not null))
            return CurrentFormatVersion;
        return all.Any(session => session.TokenStore is not null)
            ? FormatVersionWithoutComlinkKey
            : FormatVersionWithoutTokenStore;
    }
}

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
/// <c>Spectating</c> marks a spectator's membership: <c>PlayerId</c> then holds the spectator's id
/// and <c>Token</c> a spectator token, which opens the spectator door and nothing else. The game
/// keeps those in a file of their own, so a build that predates spectating never reads one as a
/// seat.
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
    bool Spectating = false)
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

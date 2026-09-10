using Rechaos.Multiplayer.Generated;

namespace Rechaos.Game;

/// <summary>Where the online flow has got to, which is what the two online screens draw from.</summary>
internal enum MultiplayerStage
{
    /// <summary>Choosing a server and a name, and whether to host or join.</summary>
    Connect,

    /// <summary>A request is in flight; the player can only wait.</summary>
    Busy,

    /// <summary>Seated in a lobby that has not started.</summary>
    Lobby,

    /// <summary>The match is running and the interface is the ordinary one.</summary>
    Playing,

    /// <summary>Planning is submitted; the turn seals when everyone is ready or the clock runs out.</summary>
    WaitingForSeal,

    /// <summary>Clients disagreed about a turn and the match is paused until it is repaired.</summary>
    Desynced,
}

/// <summary>
/// Everything the online screens need that is not the match itself.
/// </summary>
/// <remarks>
/// It is a plain mutable bag on purpose: the game loop reads it every frame to draw, and the
/// session's notices are drained into it on the same thread, so nothing here is shared with the
/// background task that talks to the server.
/// </remarks>
internal sealed class MultiplayerUiState
{
    internal MultiplayerStage Stage { get; set; } = MultiplayerStage.Connect;

    internal TextField Server { get; } = new("SERVER", 96, "http://localhost:8787");
    internal TextField DisplayName { get; } = new("NAME", 32, "PLAYER");
    internal TextField JoinCode { get; } = new("JOIN CODE", 8);

    /// <summary>The lobby as the server last described it, or null before there is one.</summary>
    internal MatchView? Match { get; set; }

    /// <summary>The code the host reads out; empty until there is a lobby.</summary>
    internal string JoinCodeShown { get; set; } = string.Empty;

    /// <summary>This client's own player id, for finding its row in the roster.</summary>
    internal string OwnPlayerId { get; set; } = string.Empty;

    /// <summary>The bearer token, held only for the life of the match.</summary>
    internal string Token { get; set; } = string.Empty;

    /// <summary>Whether this client is the host, and so the one that repairs a desync.</summary>
    internal bool IsHost { get; set; }

    /// <summary>The turn the player is planning, which is the one their orders are submitted for.</summary>
    internal int PlanningTurn { get; set; }

    /// <summary>When the open turn seals regardless of readiness, or null without a timer.</summary>
    internal DateTimeOffset? DeadlineAt { get; set; }

    /// <summary>The last thing that went wrong, for the player to read.</summary>
    internal string Status { get; set; } = string.Empty;

    /// <summary>Forgets the credentials when a match ends, so nothing outlives it.</summary>
    internal void Reset()
    {
        Stage = MultiplayerStage.Connect;
        Match = null;
        JoinCodeShown = string.Empty;
        OwnPlayerId = string.Empty;
        Token = string.Empty;
        IsHost = false;
        PlanningTurn = 0;
        DeadlineAt = null;
    }
}

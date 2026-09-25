using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class GangStatusMarkerPresentation
{
    /// <summary>The frame RULE-UI-006 returns when only a hire is on its way to the sector.</summary>
    public const int IncomingOnlyFrame = 8;

    /// <summary>
    /// RULE-UI-006 for one sector: the marker frame, or -1 for none. The circle needs a gang of
    /// the player's own in the sector; enemy presence adds 1, an idle friendly gang 2 and an
    /// incoming hire 4. Without a friendly gang an incoming hire alone gives frame 8.
    /// </summary>
    public static int Frame(bool playerPresent, bool enemySeen, bool idleGang, bool incomingHire)
    {
        var incoming = incomingHire ? 4 : 0;
        if (playerPresent)
            return (enemySeen ? 1 : 0) + (idleGang ? 2 : 0) + incoming;
        return incoming != 0 ? IncomingOnlyFrame : -1;
    }

    /// <summary>
    /// The frames left on the map after it draws every sector in number order (RULE-UI-006). The
    /// original keeps one saved cell for frame 8 and copies it back over the last sector that got
    /// frame 8 before it draws the marker of any sector without the player's presence, so frame 8
    /// stays only where no later sector lacks the player's presence.
    /// </summary>
    public static int[] MapFrames(IReadOnlyList<SectorMarkerInputs> sectors)
    {
        ArgumentNullException.ThrowIfNull(sectors);
        var frames = new int[sectors.Count];
        var savedIncomingSector = -1;
        for (var sector = 0; sector < sectors.Count; sector++)
        {
            var inputs = sectors[sector];
            if (!inputs.PlayerPresent && savedIncomingSector >= 0)
                frames[savedIncomingSector] = -1;
            frames[sector] = Frame(
                inputs.PlayerPresent, inputs.EnemySeen, inputs.IdleGang, inputs.IncomingHire);
            if (frames[sector] == IncomingOnlyFrame) savedIncomingSector = sector;
        }
        return frames;
    }

    /// <summary>
    /// RULE-UI-006's inputs for every sector, from the active player's view: the player's own
    /// active gangs are always visible to it, an enemy counts when the player can see one of its
    /// gangs, the idle test reads every friendly gang without an order, and a pending hire marks
    /// its target sector.
    /// </summary>
    public static int[] MapFrames(MatchState state, PlayerId player) =>
        MapFrames(state, player, GangSightSnapshot.Capture(state, player));

    /// <summary>
    /// RULE-UI-006 with presence and enemy sight read from <paramref name="sight"/>, the
    /// <c>gangs_seen</c> bytes as they stood when the snapshot was taken. The idle test and the
    /// incoming hires are read from the match as it is now, as the original reads the gang
    /// records and <c>hire_orders</c> on every draw.
    /// </summary>
    public static int[] MapFrames(MatchState state, PlayerId player, GangSightSnapshot sight)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(sight);
        var owner = state.FindPlayer(player) ?? throw new ArgumentOutOfRangeException(nameof(player));
        var inputs = new SectorMarkerInputs[MatchLimits.SectorCount];
        for (var sector = 0; sector < inputs.Length; sector++)
            inputs[sector] = new SectorMarkerInputs(
                sight.PlayerPresent(sector), sight.EnemySeen(sector), false, false);
        foreach (var gang in owner.Gangs.Where(gang => gang.IsActive && gang.QueuedCommand is null))
            inputs[gang.SectorId] = inputs[gang.SectorId] with { IdleGang = true };
        foreach (var pending in owner.PendingHires)
            inputs[pending.TargetSectorId] = inputs[pending.TargetSectorId] with { IncomingHire = true };
        return MapFrames(inputs);
    }

    public static Rectangle Source(
        bool hasIdleGang,
        bool hasDetectedEnemyGang,
        bool hasPendingHire)
    {
        var state = (hasDetectedEnemyGang ? 1 : 0)
            + (hasIdleGang ? 2 : 0)
            + (hasPendingHire ? 4 : 0);
        return OriginalSpriteLayout.GangStatus(state);
    }
}

/// <summary>What RULE-UI-006 reads for one sector.</summary>
public readonly record struct SectorMarkerInputs(
    bool PlayerPresent,
    bool EnemySeen,
    bool IdleGang,
    bool IncomingHire);

public static class LastTurnEventsLayout
{
    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Rectangle Page => SharedPanelLayout.At(34, 13, 47, 7);
    public static Rectangle Previous => SharedPanelLayout.At(31, 33, 26, 23);
    public static Rectangle Next => SharedPanelLayout.At(59, 33, 26, 23);
    public static Rectangle Artwork => SharedPanelLayout.At(94, 8, 242, 158);
    public static Rectangle DateValue => SharedPanelLayout.At(121, 174, 43, 7);
    public static Rectangle ObjectValue => SharedPanelLayout.At(201, 174, 135, 7);
    public static Rectangle StatusValue => SharedPanelLayout.At(135, 183, 201, 7);
    public static Rectangle ResearchItem => SharedPanelLayout.At(192, 62, 48, 48);
    public static Rectangle Ok => SharedPanelLayout.At(33, 169, 49, 22);
}

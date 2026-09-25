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

/// <summary>The three buttons of SCR-EVENT-001 that act on a release inside themselves.</summary>
public enum LastTurnEventsButton
{
    Previous,
    Next,
    Exit
}

/// <summary>
/// SCR-EVENT-001 in screen coordinates: the compositor's panel-local positions plus the panel
/// origin (104, 124) (FND-EVENT-005).
/// </summary>
public static class LastTurnEventsLayout
{
    /// <summary>
    /// The caption is loaded cut or padded to 35 characters (FND-EVENT-005, FND-EXE-005), the
    /// width of its black backing.
    /// </summary>
    public const int CaptionColumns = 35;

    public static Rectangle Panel => SharedPanelLayout.Panel;

    /// <summary>The page number, two cells at (138, 137) (SCR-EVENT-001).</summary>
    public static Rectangle PageNumber => SharedPanelLayout.At(34, 13,
        2 * OriginalFontLayout.CellWidth, OriginalFontLayout.GlyphHeight);

    /// <summary>The page count, two cells at (174, 137); the panel art's OF stays between them.</summary>
    public static Rectangle PageCount => SharedPanelLayout.At(70, 13,
        2 * OriginalFontLayout.CellWidth, OriginalFontLayout.GlyphHeight);

    public static Rectangle Previous => SharedPanelLayout.At(31, 33, 26, 23);
    public static Rectangle Next => SharedPanelLayout.At(59, 33, 26, 23);

    /// <summary>The Previous face in <c>PX00129</c>, greyed on the first page (SCR-EVENT-001).</summary>
    public static Rectangle PreviousSource(bool firstPage) => new(firstPage ? 170 : 118, 363, 26, 23);

    /// <summary>The Next face in <c>PX00129</c>, greyed on the last page (SCR-EVENT-001).</summary>
    public static Rectangle NextSource(bool lastPage) => new(lastPage ? 196 : 144, 363, 26, 23);

    /// <summary>
    /// The illustration, site picture and research monitor: panel x 94..336 and y 11..169, inside
    /// the green frame of <c>PX05010</c> (SCR-EVENT-001).
    /// </summary>
    public static Rectangle Artwork => SharedPanelLayout.At(94, 11, 242, 158);

    /// <summary>The researched item's 48-by-48 frame at (296, 190) (SCR-EVENT-001).</summary>
    public static Rectangle ResearchItem => SharedPanelLayout.At(192, 66, 48, 48);

    /// <summary>The eliminated player's portrait, stretched to 48 by 48 at (240, 237) (SCR-EVENT-001).</summary>
    public static Rectangle EliminatedPortrait => SharedPanelLayout.At(136, 113, 48, 48);

    /// <summary>The black fill under the subject, (298, 298, 138, 7) (SCR-EVENT-001).</summary>
    public static Rectangle SubjectBacking => SharedPanelLayout.At(194, 174, 138, 7);

    /// <summary>The black fill under the caption, (226, 307, 210, 7) (SCR-EVENT-001).</summary>
    public static Rectangle CaptionBacking => SharedPanelLayout.At(122, 183, 210, 7);

    /// <summary>The year, four cells at (226, 298) (SCR-EVENT-001).</summary>
    public static Rectangle Year => SharedPanelLayout.At(122, 174,
        4 * OriginalFontLayout.CellWidth, OriginalFontLayout.GlyphHeight);

    /// <summary>The week, two cells at (256, 298), after the panel art's point (SCR-EVENT-001).</summary>
    public static Rectangle Week => SharedPanelLayout.At(152, 174,
        2 * OriginalFontLayout.CellWidth, OriginalFontLayout.GlyphHeight);

    /// <summary>
    /// The subject at (298, 298). A sector label is two cells, so the separator of a two-part
    /// subject lands at (310, 298) and the second name at (316, 298) (SCR-EVENT-001).
    /// </summary>
    public static Point Subject => new(SharedPanelLayout.X(194), SharedPanelLayout.Y(174));

    /// <summary>The caption at (226, 307) (SCR-EVENT-001).</summary>
    public static Point Caption => new(SharedPanelLayout.X(122), SharedPanelLayout.Y(183));

    /// <summary>
    /// The pointer rectangle of Exit, whose plain face is part of <c>PX05010</c> (SCR-EVENT-001).
    /// </summary>
    public static Rectangle Ok => SharedPanelLayout.At(33, 169, 49, 22);

    /// <summary>The Exit face, one pixel wider and taller than its pointer rectangle (SCR-EVENT-001).</summary>
    public static Rectangle ExitFace => SharedPanelLayout.At(33, 169, 50, 23);

    /// <summary>Where a held button's pressed face is drawn (SCR-EVENT-001).</summary>
    public static Rectangle Face(LastTurnEventsButton button) => button switch
    {
        LastTurnEventsButton.Previous => Previous,
        LastTurnEventsButton.Next => Next,
        LastTurnEventsButton.Exit => ExitFace,
        _ => throw new ArgumentOutOfRangeException(nameof(button))
    };

    /// <summary>The rectangle a press starts in and a release must end in (SCR-EVENT-001).</summary>
    public static Rectangle Hit(LastTurnEventsButton button) => button switch
    {
        LastTurnEventsButton.Previous => Previous,
        LastTurnEventsButton.Next => Next,
        LastTurnEventsButton.Exit => Ok,
        _ => throw new ArgumentOutOfRangeException(nameof(button))
    };

    /// <summary>
    /// The pressed faces in <c>PX00129</c>: Previous (66, 363) and Next (92, 363) from
    /// <c>fn_00451602</c>, Exit (50, 386) from the held-button helper <c>fn_00418821</c>
    /// (SCR-EVENT-001, FND-EVENT-005).
    /// </summary>
    public static Rectangle PressedSource(LastTurnEventsButton button) => button switch
    {
        LastTurnEventsButton.Previous => new Rectangle(66, 363, 26, 23),
        LastTurnEventsButton.Next => new Rectangle(92, 363, 26, 23),
        LastTurnEventsButton.Exit => new Rectangle(50, 386, 50, 23),
        _ => throw new ArgumentOutOfRangeException(nameof(button))
    };

    public static LastTurnEventsButton? ButtonAt(Point point) =>
        Previous.Contains(point) ? LastTurnEventsButton.Previous
        : Next.Contains(point) ? LastTurnEventsButton.Next
        : Ok.Contains(point) ? LastTurnEventsButton.Exit
        : null;

    /// <summary>
    /// SCR-EVENT-001's year and week for <paramref name="elapsedTurns"/>, or null at 0, when the
    /// panel draws no date.
    /// </summary>
    public static (string Year, string Week)? Date(int elapsedTurns)
    {
        if (elapsedTurns <= 0) return null;
        var week = elapsedTurns - 1;
        return ($"{week / 52 + 2050:0000}", $"{week % 52 + 1:00}");
    }
}

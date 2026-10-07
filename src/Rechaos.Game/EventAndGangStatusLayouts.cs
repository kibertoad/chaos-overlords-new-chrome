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
    public static int[] MapFrames(MatchState state, PlayerId player, GangSightSnapshot sight) =>
        MapFrames(Inputs(state, player, sight));

    /// <summary>What RULE-UI-006 reads for every sector, as <see cref="MapFrames(MatchState, PlayerId, GangSightSnapshot)"/> reads it.</summary>
    public static SectorMarkerInputs[] Inputs(MatchState state, PlayerId player, GangSightSnapshot sight)
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
        return inputs;
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

/// <summary>
/// RULE-UI-006, FND-UI-024, FND-UI-017: the gang-status markers the city map keeps through one
/// player's planning phase. The original draws them into its map surface and redraws only what
/// changes: every sector when planning starts and when the Search panel closes, and the sectors
/// the Hire dock touches when a hire or snub order changes. Every other change of a sector holding
/// the player's gang redraws that sector's marker, so those frames follow the match. The
/// incoming-only mark, frame 8, is kept: the map shows it on the last sector without the player's
/// gangs that a redraw gave it, until any redraw of a sector without the player's gangs, the
/// dock's redraw of no sector included, copies the cell under it back. An order on the detailed
/// sector screen redraws the selected sector (FND-UI-015), and needs no step here: that screen gives
/// orders only to the player's own gangs in the sector, so the redraw always lands on a sector
/// holding them and leaves the incoming-only mark where it is.
/// </summary>
public sealed class GangStatusMarkerMap
{
    private GangSightSnapshot? _sight;
    private int[]? _hireOrders;

    /// <summary>The sector showing the incoming-only mark, or -1.</summary>
    public int IncomingMark { get; private set; } = -1;

    /// <summary>The sector the Hire dock last redrew for a hire order, or -1 (FND-HIRE-008).</summary>
    public int DockSector { get; private set; } = -1;

    /// <summary>
    /// The frames the map shows now, -1 for none. A new sight snapshot, which the snapshot cache
    /// takes once for each planning phase, player and match, draws the whole map and then the dock;
    /// a change of the player's hire orders since the last call redraws the dock.
    /// </summary>
    public int[] Frames(MatchState state, PlayerId player, GangSightSnapshot sight)
    {
        ArgumentNullException.ThrowIfNull(state);
        var inputs = GangStatusMarkerPresentation.Inputs(state, player, sight);
        var orders = HireOrders(state.FindPlayer(player)!);
        if (_hireOrders is null || !ReferenceEquals(_sight, sight))
        {
            _sight = sight;
            // FND-UI-024: fn_0046FD80 forgets the saved cell, then planning draws the map and the dock.
            IncomingMark = -1;
            DrawAll(inputs);
            RedrawDock(inputs, orders);
        }
        else if (!orders.SequenceEqual(_hireOrders))
            RedrawDock(inputs, orders);
        _hireOrders = orders;
        var frames = new int[inputs.Length];
        for (var sector = 0; sector < frames.Length; sector++)
            frames[sector] = inputs[sector].PlayerPresent
                ? GangStatusMarkerPresentation.Frame(true, inputs[sector].EnemySeen, inputs[sector].IdleGang,
                    inputs[sector].IncomingHire)
                : sector == IncomingMark ? GangStatusMarkerPresentation.IncomingOnlyFrame : -1;
        return frames;
    }

    /// <summary>The whole map drawn again, as closing the Search panel does (FND-SEARCH-004).</summary>
    public void RedrawAll(MatchState state, PlayerId player, GangSightSnapshot sight) =>
        DrawAll(GangStatusMarkerPresentation.Inputs(state, player, sight));

    /// <summary>Forgets the planning phase, so the next call draws the whole map.</summary>
    public void Clear()
    {
        _hireOrders = null;
        _sight = null;
    }

    private void DrawAll(SectorMarkerInputs[] inputs)
    {
        for (var sector = 0; sector < inputs.Length; sector++) Draw(inputs, sector);
    }

    // FND-UI-017, FND-HIRE-008: fn_00417CBA draws the previous destination's marker, keeps the
    // sector of the offer ordered into one, or -1 when none is, and draws that one's marker. A
    // Reject press therefore ends with a drawing of -1 (EXP-UI-004, EXP-UI-005).
    private void RedrawDock(SectorMarkerInputs[] inputs, int[] orders)
    {
        Draw(inputs, DockSector);
        DockSector = -1;
        foreach (var order in orders)
            if (order >= 0) DockSector = order;
        Draw(inputs, DockSector);
    }

    // FND-UI-024: fn_00412BF7 for one sector, or for -1, which lies off the map: a sector without the player's
    // gangs first gets the cell under the incoming-only mark copied back, then takes the mark when a
    // hire is on its way to it.
    private void Draw(SectorMarkerInputs[] inputs, int sector)
    {
        if (sector >= 0 && inputs[sector].PlayerPresent) return;
        IncomingMark = sector >= 0 && inputs[sector].IncomingHire ? sector : -1;
    }

    // FND-HIRE-001: hire_orders of the three offer slots, a sector, -2 for the snub or -1. A pending
    // hire without an offer slot (-1, as an older save holds it) is found by its definition, as hire
    // resolution finds it.
    private static int[] HireOrders(MatchPlayerState player)
    {
        var orders = new[] { -1, -1, -1 };
        foreach (var pending in player.PendingHires)
        {
            var slot = pending.OfferSlot;
            if (slot < 0 || slot >= orders.Length)
                slot = player.HireOfferSlots.ToList().FindIndex(offer => offer.GangDefinitionId == pending.GangDefinitionId);
            if (slot >= 0 && slot < orders.Length) orders[slot] = pending.TargetSectorId;
        }
        if (player.SnubbedHireOfferSlot is { } snubbed) orders[snubbed] = -2;
        return orders;
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
/// origin (104, 124) (FND-EVENT-007).
/// </summary>
public static class LastTurnEventsLayout
{
    /// <summary>
    /// The caption is loaded cut or padded to 35 characters (FND-EVENT-007, FND-EXE-005), the
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

    /// <summary>
    /// Where an illustration is drawn. The original uploads every illustration as 158 bottom-up
    /// rows (FND-GFX-005), so a picture with fewer rows, such as PX06008's 157, keeps its native
    /// scale along the bottom edge of <see cref="Artwork"/>, and the rows above it stay black
    /// (DEV-GFX-002). A full-size picture fills the whole area.
    /// </summary>
    public static Rectangle ArtworkDestination(int textureWidth, int textureHeight)
    {
        var area = Artwork;
        return textureWidth == area.Width && textureHeight > 0 && textureHeight < area.Height
            ? new Rectangle(area.X, area.Bottom - textureHeight, area.Width, textureHeight)
            : area;
    }

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
    /// <c>fn_00451602</c>, and the lit Exit face (0, 386) from the held-button helper
    /// <c>fn_00418821</c> (SCR-EVENT-001, FND-EVENT-007, FND-UI-062).
    /// </summary>
    public static Rectangle PressedSource(LastTurnEventsButton button) => button switch
    {
        LastTurnEventsButton.Previous => new Rectangle(66, 363, 26, 23),
        LastTurnEventsButton.Next => new Rectangle(92, 363, 26, 23),
        LastTurnEventsButton.Exit => HeldButtonFaces.Lit(HeldButtonKind.Confirm),
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
        var (year, week) = MatchCalendar.Of(elapsedTurns - 1);
        return ($"{year:0000}", $"{week:00}");
    }
}

using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;

namespace Rechaos.Game;

/// <summary>
/// A request to show a saved match at the planning entry it stands at and write the drawing
/// area to a file, for tests that compare the rebuild's screens with captures of the original.
/// </summary>
/// <param name="SavePath">
/// A native save of a match standing at a planning entry, as a replayed experiment run ends, or
/// one of <see cref="ScreenOperands"/> for a screen shown before a match.
/// </param>
/// <param name="OutputPath">The 640-by-460, 32-bit, top-down bitmap to write.</param>
/// <param name="MarkerFrame">
/// The frame of the Overlord bar's marker the capture showed (FND-UI-038), in place of the one
/// the clock gives.
/// </param>
/// <param name="Clicks">
/// Left-button clicks made on the planning entry before the frame is drawn, in order, so the
/// frame can show a panel or view a capture of the original was taken with.
/// </param>
/// <param name="PumpCounter">
/// The pump's counter the capture recorded (FND-UI-017), which picks the selection frame drawn
/// (FND-UI-048), in place of the clock's.
/// </param>
/// <param name="ItemFrame">
/// The frame of the rotating item pictures of Item Information, Sell or Give the capture showed
/// (FND-UI-052, FND-UI-053), in place of the one the clock gives.
/// </param>
/// <param name="IdlePhase">
/// The idle gang warning's ticks since its open modulo 8 the capture showed, which pick whether
/// its line blinks on (FND-UI-054), in place of the ones the clock gives.
/// </param>
/// <param name="CaretPhase">
/// The Comlink Send caret's timer events since its last flip the capture showed, 0 to 2 after a
/// flip to plain and 3 to 5 after a flip to inverse (FND-COMLINK-010), in place of the clock's.
/// </param>
/// <param name="ClipTick">
/// The tick of the Detailed Combat clip the capture showed (FND-COMBAT-016), which the clip the
/// clicks started is drawn at, in place of its first.
/// </param>
/// <param name="ClipIndex">
/// The index within its presentation of the Detailed Combat clip the capture showed, counted from
/// 0 (FND-COMBAT-011): the frame passes over that many of the presentation's clips before it
/// applies <see cref="ClipTick"/>. Null is the first clip.
/// </param>
/// <param name="SelectedSector">
/// The sector the capture had selected (FND-SAVE-003), in place of the one the planning entry
/// restores, since the save does not keep it (DEV-SAVE-001).
/// </param>
/// <param name="Lamps">
/// Whether the capture showed the Events and Comlink lamps drawn lit (FND-EVENT-006), which picks
/// the blink phase of those lights in place of the clock's.
/// </param>
public sealed record ReferenceFrameRequest(
    string SavePath, string OutputPath, int? MarkerFrame = null, IReadOnlyList<ReferenceClick>? Clicks = null,
    int? PumpCounter = null, int? SelectedSector = null, ReferenceLamps? Lamps = null, int? ItemFrame = null,
    int? ClipTick = null, int? IdlePhase = null, int? CaretPhase = null, int? ClipIndex = null)
{
    /// <summary>
    /// The operands that ask for a screen shown before a match in place of a save: the title
    /// screen (SCR-UI-001), the credits over it (SCR-UI-002) and the local setup New Game opens
    /// first (SCR-SETUP-001).
    /// </summary>
    public static readonly IReadOnlyList<string> ScreenOperands = ["title", "credits", "setup"];

    private const string Usage =
        "Usage: --reference-frame <save|title|credits|setup> <bitmap> [--marker-frame <0-11>] [--pump-counter <0-7>]"
        + " [--selected-sector <0-63>] [--lamps <0|1>,<0|1>] [--item-frame <0-14>] [--idle-phase <0-7>]"
        + " [--caret-phase <0-5>] [--clip-tick <0-21> [--clip-index <n>]]"
        + " [--reference-clicks <x:y[:2]|x:y>x:y|'TEXT>,...]";

    // Keep captures independent of the player's preferences, recovery files and saves. The
    // directory sits beside the bitmap and is kept after exit, so a failed run can be diagnosed
    // from its logs and the caller removes it with the bitmap.
    public string UserDataDirectory { get; } = Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(OutputPath))!,
        "rechaos-reference-frame-" + Guid.NewGuid().ToString("N"));

    /// <summary>The screen shown before a match the frame draws, or null for a saved match.</summary>
    public string? Screen => ScreenOperands.Contains(SavePath) ? SavePath : null;

    public static ReferenceFrameRequest? ParseArguments(string[] args)
    {
        var reference = Array.IndexOf(args, "--reference-frame");
        var marker = Array.IndexOf(args, "--marker-frame");
        var clicks = Array.IndexOf(args, "--reference-clicks");
        var pump = Array.IndexOf(args, "--pump-counter");
        var selected = Array.IndexOf(args, "--selected-sector");
        var lamps = Array.IndexOf(args, "--lamps");
        var item = Array.IndexOf(args, "--item-frame");
        var tick = Array.IndexOf(args, "--clip-tick");
        var idle = Array.IndexOf(args, "--idle-phase");
        var caret = Array.IndexOf(args, "--caret-phase");
        var clip = Array.IndexOf(args, "--clip-index");
        if (reference < 0)
        {
            if (tick >= 0) throw new ArgumentException("--clip-tick requires --reference-frame.");
            if (clip >= 0) throw new ArgumentException("--clip-index requires --reference-frame.");
            if (item >= 0) throw new ArgumentException("--item-frame requires --reference-frame.");
            if (idle >= 0) throw new ArgumentException("--idle-phase requires --reference-frame.");
            if (caret >= 0) throw new ArgumentException("--caret-phase requires --reference-frame.");
            if (selected >= 0) throw new ArgumentException("--selected-sector requires --reference-frame.");
            if (marker >= 0) throw new ArgumentException("--marker-frame requires --reference-frame.");
            if (clicks >= 0) throw new ArgumentException("--reference-clicks requires --reference-frame.");
            if (pump >= 0) throw new ArgumentException("--pump-counter requires --reference-frame.");
            if (lamps >= 0) throw new ArgumentException("--lamps requires --reference-frame.");
            return null;
        }
        if (Array.LastIndexOf(args, "--reference-frame") != reference
            || (marker >= 0 && Array.LastIndexOf(args, "--marker-frame") != marker)
            || (clicks >= 0 && Array.LastIndexOf(args, "--reference-clicks") != clicks)
            || (pump >= 0 && Array.LastIndexOf(args, "--pump-counter") != pump)
            || (selected >= 0 && Array.LastIndexOf(args, "--selected-sector") != selected)
            || (lamps >= 0 && Array.LastIndexOf(args, "--lamps") != lamps)
            || (item >= 0 && Array.LastIndexOf(args, "--item-frame") != item)
            || (tick >= 0 && Array.LastIndexOf(args, "--clip-tick") != tick)
            || (idle >= 0 && Array.LastIndexOf(args, "--idle-phase") != idle)
            || (caret >= 0 && Array.LastIndexOf(args, "--caret-phase") != caret)
            || (clip >= 0 && Array.LastIndexOf(args, "--clip-index") != clip))
            throw new ArgumentException("Capture options may only be supplied once.");
        static string Operand(string[] values, int index)
        {
            if (index >= values.Length || string.IsNullOrWhiteSpace(values[index])
                || values[index].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException(Usage);
            return values[index];
        }
        var source = Operand(args, reference + 1);
        var beforeMatch = ScreenOperands.Contains(source);
        var save = beforeMatch ? source : Path.GetFullPath(source);
        // The marker, pump, selected sector, lamps, item frame, idle and caret phases and the clip
        // belong to a match's screens, which a screen shown before a match does not draw.
        if (beforeMatch && (marker >= 0 || pump >= 0 || selected >= 0 || lamps >= 0 || item >= 0 || tick >= 0
                            || idle >= 0 || caret >= 0 || clip >= 0))
            throw new ArgumentException(
                "--marker-frame, --pump-counter, --selected-sector, --lamps, --item-frame, --idle-phase, --caret-phase,"
                + " --clip-tick and --clip-index require a save.");
        // A clip is drawn at a tick, so its index alone gives nothing to draw.
        if (clip >= 0 && tick < 0) throw new ArgumentException("--clip-index requires --clip-tick.");
        var output = Path.GetFullPath(Operand(args, reference + 2));
        int? frame = null;
        if (marker >= 0)
        {
            // FND-UI-038: the marker counter wraps after its twelve frames.
            if (!int.TryParse(Operand(args, marker + 1),
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var value)
                || value is < 0 or > 11)
                throw new ArgumentException("--marker-frame must be between 0 and 11.");
            frame = value;
        }
        if (string.Equals(save, output, OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new ArgumentException("The capture bitmap must not overwrite the input save.");
        int? counter = null;
        if (pump >= 0)
        {
            // FND-UI-017: the pump counts from 0 to 7.
            if (!int.TryParse(Operand(args, pump + 1),
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var value)
                || value is < 0 or > 7)
                throw new ArgumentException("--pump-counter must be between 0 and 7.");
            counter = value;
        }
        int? sector = null;
        if (selected >= 0)
        {
            if (!int.TryParse(Operand(args, selected + 1),
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var value)
                || value >= MatchLimits.SectorCount)
                throw new ArgumentException("--selected-sector must be between 0 and 63.");
            sector = value;
        }
        int? itemFrame = null;
        if (item >= 0)
        {
            // FND-UI-052, FND-UI-053: the items turn through their fifteen frames.
            if (!int.TryParse(Operand(args, item + 1),
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var value)
                || value >= ItemRotationPresentation.FrameCount)
                throw new ArgumentException("--item-frame must be between 0 and 14.");
            itemFrame = value;
        }
        int? clipTick = null;
        if (tick >= 0)
        {
            // FND-COMBAT-016: a clip ends on tick 22, so the screen shows ticks 0 to 21.
            if (!int.TryParse(Operand(args, tick + 1),
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var value)
                || value >= CombatAnimationRouting.CompletionTick)
                throw new ArgumentException("--clip-tick must be between 0 and 21.");
            clipTick = value;
        }
        int? Bounded(int at, string name, int limit)
        {
            if (at < 0) return null;
            if (!int.TryParse(Operand(args, at + 1),
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var value)
                || value >= limit)
                throw new ArgumentException(limit == int.MaxValue
                    ? $"{name} must be 0 or more."
                    : $"{name} must be between 0 and {limit - 1}.");
            return value;
        }
        // FND-UI-054: the warning's line repeats every eight ticks.
        var idlePhase = Bounded(idle, "--idle-phase", 8);
        // FND-COMLINK-010: the caret flips every third timer event, so its cycle is six.
        var caretPhase = Bounded(caret, "--caret-phase", 2 * ComlinkCaretCadence.EventsPerGlyphRow);
        var clipIndex = Bounded(clip, "--clip-index", int.MaxValue);
        return new ReferenceFrameRequest(save, output, frame,
            clicks >= 0 ? ReferenceClick.ParseList(Operand(args, clicks + 1)) : null, counter, sector,
            lamps >= 0 ? ReferenceLamps.Parse(Operand(args, lamps + 1)) : null, itemFrame, clipTick,
            idlePhase, caretPhase, clipIndex);
    }
}

/// <summary>
/// Whether the Events and the Comlink lamp were drawn lit when the capture was taken: the bytes
/// <c>0x00487818</c> and <c>0x00487820</c> the pump sets when it draws a lamp lit and clears when
/// it restores the control (FND-EVENT-006).
/// </summary>
public sealed record ReferenceLamps(bool Events, bool Comlink)
{
    public static ReferenceLamps Parse(string value) => value.Split(',') switch
    {
        ["0" or "1", "0" or "1"] parts => new ReferenceLamps(parts[0] == "1", parts[1] == "1"),
        _ => throw new ArgumentException("--lamps is <events>,<comlink>, each 0 or 1."),
    };

    public override string ToString() => $"{(Events ? 1 : 0)},{(Comlink ? 1 : 0)}";
}

/// <summary>
/// A left-button click at a point of the drawing area, made twice for a double-click, or a press
/// there that moves to <see cref="Release"/> with the button held and is released there.
/// </summary>
public sealed record ReferenceClick(Point Point, bool Double = false, Point? Release = null)
{
    /// <summary>
    /// Text typed in place of a click, a character at a time, as the Comlink Send panel takes keys:
    /// upper-case letters, digits and spaces, written <c>'TEXT</c> in the list.
    /// </summary>
    public string? Text { get; init; }

    public static IReadOnlyList<ReferenceClick> ParseList(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(entry =>
        {
            if (entry.StartsWith('\''))
                return entry.Length > 1
                       && entry.Skip(1).All(character => character is ' ' or (>= '0' and <= '9') or (>= 'A' and <= 'Z'))
                    ? new ReferenceClick(Point.Zero) { Text = entry[1..] }
                    : throw new ArgumentException($"Typed text is 'TEXT of upper-case letters, digits and spaces: {entry}");
            var ends = entry.Split('>');
            var numbers = ends.SelectMany(end => end.Split(':')).Select(part => int.TryParse(part,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var number) ? number : -1).ToArray();
            return (ends.Length, numbers) switch
            {
                (1, [>= 0 and < VirtualInput.Width, >= 0 and < VirtualInput.Height]) =>
                    new ReferenceClick(new Point(numbers[0], numbers[1])),
                (1, [>= 0 and < VirtualInput.Width, >= 0 and < VirtualInput.Height, 2]) =>
                    new ReferenceClick(new Point(numbers[0], numbers[1]), Double: true),
                (2, [>= 0 and < VirtualInput.Width, >= 0 and < VirtualInput.Height,
                    >= 0 and < VirtualInput.Width, >= 0 and < VirtualInput.Height]) =>
                    new ReferenceClick(new Point(numbers[0], numbers[1]), Release: new Point(numbers[2], numbers[3])),
                _ => throw new ArgumentException(
                    $"A reference click is x:y, x:y:2 or x:y>x:y inside the drawing area: {entry}"),
            };
        }).ToArray();

    public override string ToString() => Text is not null ? "'" + Text : Release is { } release
        ? $"{Point.X}:{Point.Y}>{release.X}:{release.Y}"
        : $"{Point.X}:{Point.Y}" + (Double ? ":2" : "");
}

public sealed partial class ChaosGame
{
    // The frames drawn before the capture, so textures and layouts loaded on the first draw have
    // settled.
    private const int ReferenceFrameWarmUpDraws = 3;

    // The scripted clicks run on a clock of their own, one button edge per update, and the frame
    // is drawn once the last one has had a second to settle, so a pressed face's wait and a
    // flash (RULE-TIMER-004) are over.
    private static readonly TimeSpan ReferenceClickStep = TimeSpan.FromMilliseconds(50);
    private const int ReferenceSettleUpdates = 20;

    private readonly ReferenceFrameRequest? _referenceFrame;
    private int _referenceFrameDraws = -1;
    private IReadOnlyList<(Point Point, ReferenceEdge Edge, char Character)> _referenceEdges = [];

    private enum ReferenceEdge
    {
        Press,
        Move,
        Release,
        Type,
    }
    private int _referenceEdge;
    private int _referenceSettled;
    private TimeSpan _referenceClock;

    /// <summary>
    /// Enters the saved match, or shows the screen named in its place, once and then takes the
    /// update loop over, so no input but the scripted clicks and no clock but theirs moves the
    /// screen while the frame is drawn.
    /// </summary>
    private bool UpdateReferenceFrame()
    {
        if (_referenceFrame is null) return false;
        if (_referenceFrameDraws < 0 && _definitions is not null)
        {
            // Panels are drawn in place, as the original's captures show them once slid in.
            _slidePanels = false;
            _panelSlideTransition.Clear();
            switch (_referenceFrame.Screen)
            {
                case "title":
                    _screens.Show(ClientScreen.Title);
                    break;
                case "credits":
                    _screens.Show(ClientScreen.Title);
                    OpenCredits();
                    break;
                case "setup":
                    OpenNewGameSetup();
                    break;
                default:
                    EnterNewMatch(NativeSaveStore.Load(_referenceFrame.SavePath, _definitions),
                        advanceToPlanning: false);
                    break;
            }
            _referenceEdges = (_referenceFrame.Clicks ?? [])
                .SelectMany(click => click.Text is { } text
                    ? text.Select(character => (Point.Zero, ReferenceEdge.Type, character))
                    : click.Release is { } release
                    ?
                    [
                        (click.Point, ReferenceEdge.Press, '\0'), (release, ReferenceEdge.Move, '\0'),
                        (release, ReferenceEdge.Release, '\0'),
                    ]
                    : Enumerable.Repeat(click.Point, click.Double ? 2 : 1)
                        .SelectMany(point => new[] { (point, ReferenceEdge.Press, '\0'), (point, ReferenceEdge.Release, '\0') }))
                .ToArray();
            _referenceFrameDraws = 0;
            return true;
        }
        if (_referenceFrameDraws >= 0 && !ReferenceClicksSettled) StepReferenceClicks();
        // The reference frame's clock never advances a clip, so the presentation its clicks started
        // stands at the capture's clip and tick. Clicks that start no clip would draw the screen
        // without the panel.
        if (_referenceFrame.ClipTick is { } tick)
        {
            if (_combatAnimationPlayer.IsPlaying) _combatAnimationPlayer.Show(_referenceFrame.ClipIndex ?? 0, tick);
            else if (ReferenceClicksSettled)
                throw new InvalidOperationException("--clip-tick was given, but the reference clicks started no Detailed Combat clip.");
        }
        return true;
    }

    private bool ReferenceClicksSettled =>
        _referenceEdges.Count == 0 || _referenceSettled >= ReferenceSettleUpdates;

    private void StepReferenceClicks()
    {
        _referenceClock += ReferenceClickStep;
        _inputTime = _referenceClock;
        _eventPump.Update(_inputTime, OutsideEventPump());
        // A pressed face or a flash takes the input of the frames it waits through.
        if (UpdateTickedPresentation()) return;
        if (_referenceEdge < _referenceEdges.Count)
        {
            var (point, edge, character) = _referenceEdges[_referenceEdge++];
            // The live loop puts the pointer in the hover point before it handles a press or a
            // move, and a held button draws its pressed face only under it. After the release the
            // reference frame shows no pointer, as before its clicks, so nothing is drawn as
            // hovered.
            switch (edge)
            {
                case ReferenceEdge.Press:
                    UpdateHoverPoint(point);
                    _dragPoint = point;
                    HandleClick(point);
                    break;
                case ReferenceEdge.Type:
                    // A key typed in the Comlink Send panel (RULE-COMLINK-006). Text typed while
                    // another screen shows would be lost and the frame drawn without it.
                    if (_screens.Current != ClientScreen.ComlinkSend)
                        throw new InvalidOperationException(
                            $"Typed text reached the {_screens.Current} screen; it is typed into Comlink Send.");
                    TypeComlinkCharacter(character);
                    break;
                case ReferenceEdge.Move:
                    UpdateHoverPoint(point);
                    _dragPoint = point;
                    HoldPointerAt(point);
                    break;
                default:
                    CompletePointerRelease(pointerMapped: true, point, rightButton: false);
                    UpdateHoverPoint(null);
                    break;
            }
            return;
        }
        _referenceSettled++;
    }

    /// <summary>
    /// The time the blinking and cycling parts of the screen (the item rotation and the idle-gang
    /// warning's line among them) are drawn at: the reference frame draws them as at time zero,
    /// whatever its clicks advanced the clock to.
    /// </summary>
    private TimeSpan PresentationDrawTime => _referenceFrame is null ? _eventPump.Time : TimeSpan.Zero;

    /// <summary>
    /// FND-UI-051: the selection frame a slid-in panel holds, the one shown when it came in, or
    /// null while no panel is open.
    /// </summary>
    private int? _heldSelectionFrame;

    /// <summary>
    /// The time the empty seats' animation is drawn at: the reference frame draws it as at time
    /// zero, so the frame does not depend on how many clicks it made.
    /// </summary>
    private TimeSpan PresentationInputTime => _referenceFrame is null ? _inputTime : TimeSpan.Zero;

    /// <summary>
    /// Whether a light whose flag is set is in its lit phase (FND-EVENT-006): the phase the
    /// reference frame's capture recorded for its lamp, otherwise the clock's.
    /// </summary>
    private bool LampInLitPhase(bool? recorded) => recorded ?? PresentationClock.BlinkLit(PresentationDrawTime);

    /// <summary>
    /// The selection frame the pump has drawn last (FND-UI-017): the one for the reference frame's
    /// recorded counter (FND-UI-048), otherwise the one a slid-in panel holds (FND-UI-051), otherwise
    /// the one for the clock.
    /// </summary>
    private int SelectionFrameShown() => _referenceFrame?.PumpCounter is { } counter
        ? CityMapLayout.SelectionFrameAfterPass(counter)
        : _heldSelectionFrame ?? CityMapLayout.SelectionFrame(PresentationDrawTime);

    /// <summary>
    /// FND-UI-051: the frame held after the screen moves from <paramref name="previous"/> to
    /// <paramref name="current"/>. A panel coming in holds the frame <paramref name="shown"/>; a
    /// panel replacing another keeps the held one, as no pass of the pump runs between the
    /// slide-out and the slide-in; any other screen releases it.
    /// </summary>
    public static int? HeldSelectionFrame(ClientScreen previous, ClientScreen current, int? held, int shown) =>
        !PanelSlideTransition.IsPanel(current) ? null
        : PanelSlideTransition.IsPanel(previous) ? held ?? shown
        : shown;

    /// <summary>
    /// Shows the city of the player whose planning entry the save stands at, or the endgame
    /// (SCR-AWARDS-001) when the save's match is decided. With several local
    /// humans the planning entry opens the hand-off card first, as the original's does
    /// (SCR-SETUP-002), and its Ready goes on as in play. Otherwise Combat Results and Last Turn
    /// Events that the planning entry would open first are not drawn, because the comparison only
    /// covers the city screen and its console, but Last Turn Events is closed as a press of its
    /// Exit closes it: its first page counts as shown, so the Events light stays lit only while
    /// another report is unseen (RULE-EVENT-005). Hire offers, the Comlink alert and the planning
    /// timer are prepared as the planning entry prepares them when it goes straight to the city.
    /// A save whose last resolution eliminated a local human opens that human's elimination card
    /// (SCR-OBJECTIVE-002), and a save of a decided match the endgame (SCR-AWARDS-001).
    /// </summary>
    private void PresentReferenceFramePlanningEntry()
    {
        // SCR-OBJECTIVE-002: a save where the last resolution eliminated a local human stands at
        // its card, at the place that player's planning would have come (RULE-OBJECTIVE-005). A
        // human eliminated in an earlier turn has had its card, as in play. A card whose slot comes
        // after the human whose planning the save stands at is not reached yet: play shows it only
        // when the planning advance crosses that slot.
        if (_state is not null)
        {
            var viewer = PlanningViewer is { } active
                && _state.FindPlayer(active) is { Status: PlayerStatus.Active } activePlayer
                && activePlayer.Setup.Controller == PlayerController.Human
                ? active
                : (PlayerId?)null;
            var eliminated = HotSeatHandoffPresentation
                .PlayersEliminatedSince(_state, _state.Coordinator.Turn - 1)
                .Where(id => _state.FindPlayer(id)?.Setup.Controller == PlayerController.Human
                    && (viewer is null || id.Value < viewer.Value.Value))
                .OrderBy(id => id.Value);
            foreach (var id in HotSeatEliminationPresentation.QueueUnpresented(
                         eliminated, _presentedHotSeatEliminations))
                _pendingHotSeatEliminations.Enqueue(id);
            if (ShowPendingHotSeatElimination()) return;
        }
        // SCR-AWARDS-001: a save of a decided match stands where the original shows the endgame.
        if (_state?.Outcome is not null)
        {
            ShowMatchEnd(justEnded: false);
            return;
        }
        if (PlanningViewer is { } playerId && _state is { } state
            && state.FindPlayer(playerId)?.Setup.Controller == PlayerController.Human)
        {
            if (_referenceFrame?.SelectedSector is { } selected)
                _planningSelections.Store(playerId, selected);
            // FND-SAVE-003: the planning player's own sector, as PresentHotSeatPlanningEntry picks it.
            _cursor = _planningSelections.For(playerId, _cursor);
            if (HotSeatHandoffPresentation.RequiresPrivateHandoff(state))
            {
                _screens.Show(ClientScreen.Handoff);
                return;
            }
            PrepareCurrentHireOffers();
            _deferComlinkAlertUntilPlanningVisible = true;
            _managementReturnScreen = ClientScreen.City;
            if (LastTurnReports(state, playerId).Count > 0)
            {
                BeginEventReview(ReviewableReports(state, playerId).Count);
                CloseEvents();
                return;
            }
            _screens.Show(ClientScreen.City);
            CompletePlanningEntryPresentation();
            return;
        }
        _screens.Show(ClientScreen.City);
    }

    private void CaptureReferenceFrame()
    {
        if (_referenceFrame is null || _referenceFrameDraws < 0 || !ReferenceClicksSettled) return;
        if (++_referenceFrameDraws < ReferenceFrameWarmUpDraws) return;
        var width = GraphicsDevice.PresentationParameters.BackBufferWidth;
        var height = GraphicsDevice.PresentationParameters.BackBufferHeight;
        // Any other size letterboxes or scales the drawing area, so a crop of it is not the frame.
        if (width != VirtualInput.Width || height != VirtualInput.Height)
            throw new InvalidOperationException(
                $"The back buffer is {width} by {height}, not {VirtualInput.Width} by {VirtualInput.Height}.");
        var pixels = new Color[checked(width * height)];
        GraphicsDevice.GetBackBufferData(pixels);
        var bgra = new byte[VirtualInput.Width * VirtualInput.Height * 4];
        for (var y = 0; y < VirtualInput.Height; y++)
        for (var x = 0; x < VirtualInput.Width; x++)
        {
            var colour = pixels[y * width + x];
            var at = (y * VirtualInput.Width + x) * 4;
            bgra[at] = colour.B;
            bgra[at + 1] = colour.G;
            bgra[at + 2] = colour.R;
        }
        using (var stream = File.Create(_referenceFrame.OutputPath))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write((byte)'B');
            writer.Write((byte)'M');
            writer.Write(54 + bgra.Length);
            writer.Write(0);
            writer.Write(54);
            writer.Write(40);
            writer.Write(VirtualInput.Width);
            writer.Write(-VirtualInput.Height);
            writer.Write((short)1);
            writer.Write((short)32);
            writer.Write(0);
            writer.Write(bgra.Length);
            writer.Write(0L);
            writer.Write(0L);
            writer.Write(bgra);
        }
        // The frame's match was loaded without being marked saved, and the update loop stops
        // short of the game menu here, so a save-first prompt (RULE-UI-015) would hold the
        // process open until the capture times out.
        _exitConfirmed = true;
        Exit();
    }
}

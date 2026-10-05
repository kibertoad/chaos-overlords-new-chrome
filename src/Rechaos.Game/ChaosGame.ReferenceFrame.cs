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
/// (FND-UI-052, FND-UI-053), or the idle gang warning's ticks since its open modulo 8, which
/// pick whether its line blinks on (FND-UI-054), in place of the one the clock gives.
/// </param>
/// <param name="SelectedSector">
/// The sector the capture had selected (FND-SAVE-003), in place of the one the planning entry
/// restores, since the save does not keep it (DEV-SAVE-001).
/// </param>
public sealed record ReferenceFrameRequest(
    string SavePath, string OutputPath, int? MarkerFrame = null, IReadOnlyList<ReferenceClick>? Clicks = null,
    int? PumpCounter = null, int? SelectedSector = null, int? ItemFrame = null)
{
    /// <summary>
    /// The operands that ask for a screen shown before a match in place of a save: the title
    /// screen (SCR-UI-001), the credits over it (SCR-UI-002) and the local setup New Game opens
    /// first (SCR-SETUP-001).
    /// </summary>
    public static readonly IReadOnlyList<string> ScreenOperands = ["title", "credits", "setup"];

    private const string Usage =
        "Usage: --reference-frame <save|title|credits|setup> <bitmap> [--marker-frame <0-11>] [--pump-counter <0-7>]"
        + " [--selected-sector <0-63>] [--item-frame <0-14>] [--reference-clicks <x:y[:2]|x:y>x:y>,...]";

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
        var item = Array.IndexOf(args, "--item-frame");
        if (reference < 0)
        {
            if (item >= 0) throw new ArgumentException("--item-frame requires --reference-frame.");
            if (selected >= 0) throw new ArgumentException("--selected-sector requires --reference-frame.");
            if (marker >= 0) throw new ArgumentException("--marker-frame requires --reference-frame.");
            if (clicks >= 0) throw new ArgumentException("--reference-clicks requires --reference-frame.");
            if (pump >= 0) throw new ArgumentException("--pump-counter requires --reference-frame.");
            return null;
        }
        if (Array.LastIndexOf(args, "--reference-frame") != reference
            || (marker >= 0 && Array.LastIndexOf(args, "--marker-frame") != marker)
            || (clicks >= 0 && Array.LastIndexOf(args, "--reference-clicks") != clicks)
            || (pump >= 0 && Array.LastIndexOf(args, "--pump-counter") != pump)
            || (selected >= 0 && Array.LastIndexOf(args, "--selected-sector") != selected)
            || (item >= 0 && Array.LastIndexOf(args, "--item-frame") != item))
            throw new ArgumentException("Capture options may only be supplied once.");
        static string Operand(string[] values, int index)
        {
            if (index >= values.Length || string.IsNullOrWhiteSpace(values[index])
                || values[index].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException(Usage);
            return values[index];
        }
        var source = Operand(args, reference + 1);
        var save = ScreenOperands.Contains(source) ? source : Path.GetFullPath(source);
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
            // FND-UI-052: the item turns through its fifteen frames.
            if (!int.TryParse(Operand(args, item + 1),
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var value)
                || value >= ItemRotationPresentation.FrameCount)
                throw new ArgumentException("--item-frame must be between 0 and 14.");
            itemFrame = value;
        }
        return new ReferenceFrameRequest(save, output, frame,
            clicks >= 0 ? ReferenceClick.ParseList(Operand(args, clicks + 1)) : null, counter, sector, itemFrame);
    }
}

/// <summary>
/// A left-button click at a point of the drawing area, made twice for a double-click, or a press
/// there that moves to <see cref="Release"/> with the button held and is released there.
/// </summary>
public sealed record ReferenceClick(Point Point, bool Double = false, Point? Release = null)
{
    public static IReadOnlyList<ReferenceClick> ParseList(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(entry =>
        {
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

    public override string ToString() => Release is { } release
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
    private IReadOnlyList<(Point Point, ReferenceEdge Edge)> _referenceEdges = [];

    private enum ReferenceEdge
    {
        Press,
        Move,
        Release,
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
                .SelectMany(click => click.Release is { } release
                    ?
                    [
                        (click.Point, ReferenceEdge.Press), (release, ReferenceEdge.Move),
                        (release, ReferenceEdge.Release),
                    ]
                    : Enumerable.Repeat(click.Point, click.Double ? 2 : 1)
                        .SelectMany(point => new[] { (point, ReferenceEdge.Press), (point, ReferenceEdge.Release) }))
                .ToArray();
            _referenceFrameDraws = 0;
            return true;
        }
        if (_referenceFrameDraws >= 0 && !ReferenceClicksSettled) StepReferenceClicks();
        return true;
    }

    private bool ReferenceClicksSettled =>
        _referenceEdges.Count == 0 || _referenceSettled >= ReferenceSettleUpdates;

    private void StepReferenceClicks()
    {
        _referenceClock += ReferenceClickStep;
        _inputTime = _referenceClock;
        _eventPump.Update(_inputTime, holding: false);
        // A pressed face or a flash takes the input of the frames it waits through.
        if (UpdateTickedPresentation()) return;
        if (_referenceEdge < _referenceEdges.Count)
        {
            var (point, edge) = _referenceEdges[_referenceEdge++];
            switch (edge)
            {
                case ReferenceEdge.Press:
                    _dragPoint = point;
                    HandleClick(point);
                    break;
                case ReferenceEdge.Move:
                    _dragPoint = point;
                    HoldPointerAt(point);
                    break;
                default:
                    CompletePointerRelease(pointerMapped: true, point, rightButton: false);
                    break;
            }
            return;
        }
        _referenceSettled++;
    }

    /// <summary>
    /// The time the blinking and cycling parts of the screen are drawn at: the reference frame
    /// draws them as at time zero, whatever its clicks advanced the clock to.
    /// </summary>
    private TimeSpan PresentationDrawTime => _referenceFrame is null ? _eventPump.Time : TimeSpan.Zero;

    /// <summary>
    /// The selection frame the pump has drawn last (FND-UI-017): the one for the reference frame's
    /// recorded counter (FND-UI-048), otherwise the one for the clock.
    /// </summary>
    private int SelectionFrameShown() => _referenceFrame?.PumpCounter is { } counter
        ? CityMapLayout.SelectionFrameAfterPass(counter)
        : CityMapLayout.SelectionFrame(PresentationDrawTime);

    /// <summary>
    /// Shows the city of the player whose planning entry the save stands at. With several local
    /// humans the planning entry opens the hand-off card first, as the original's does
    /// (SCR-SETUP-002), and its Ready goes on as in play. Otherwise Combat Results and Last Turn
    /// Events that the planning entry would open first are not drawn, because the comparison only
    /// covers the city screen and its console, but Last Turn Events is closed as a press of its
    /// Exit closes it: its first page counts as shown, so the Events light stays lit only while
    /// another report is unseen (RULE-EVENT-005). Hire offers, the Comlink alert and the planning
    /// timer are prepared as the planning entry prepares them when it goes straight to the city.
    /// </summary>
    private void PresentReferenceFramePlanningEntry()
    {
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
            {
                _planningSelections.Store(playerId, selected);
                _cursor = selected;
            }
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
        Exit();
    }
}

using System.Globalization;
using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;

namespace Rechaos.Game;

/// <summary>What a key or a control of the replay viewer asks for.</summary>
internal enum ReplayViewerCommand
{
    Start,
    PreviousTurn,
    PreviousStep,
    TogglePlay,
    NextStep,
    NextTurn,
    End,
    Faster,
    Slower,
    Exit
}

/// <summary>
/// The replay viewer's transport: which step is shown, whether it plays on by itself, and how fast.
/// </summary>
/// <remarks>
/// It only moves a <see cref="MatchReplayPlayback"/>, which verified the journal when it was
/// opened and checks every step it applies again, so nothing here can reach the live match or
/// change what a recorded step produces. A step that fails its check surfaces as the
/// <see cref="InvalidDataException"/> the cursor throws, for the caller to close the viewer on.
/// </remarks>
internal sealed class ReplayViewer
{
    /// <summary>The playback speeds, as multiples of <see cref="SecondsPerShownStep"/>.</summary>
    public static readonly IReadOnlyList<double> Speeds = [0.25, 0.5, 1, 2, 4, 8];

    /// <summary>The speed a viewer opens at: one shown step per <see cref="SecondsPerShownStep"/>.</summary>
    public const int DefaultSpeedIndex = 2;

    /// <summary>How long a step that changes what the board shows stays on screen at 1X.</summary>
    public const double SecondsPerShownStep = 0.4;

    /// <summary>
    /// The most steps one update applies, so a slow frame at 8X catches up over several frames
    /// instead of stalling on one.
    /// </summary>
    public const int MaximumStepsPerUpdate = 64;

    private double _elapsed;

    public ReplayViewer(MatchReplayPlayback playback, ReplayFailure? primaryFailure = null)
    {
        Playback = playback ?? throw new ArgumentNullException(nameof(playback));
        PrimaryFailure = primaryFailure;
    }

    public MatchReplayPlayback Playback { get; }

    /// <summary>Why the primary journal could not be played, when the viewer shows its backup.</summary>
    public ReplayFailure? PrimaryFailure { get; }

    public bool Playing { get; private set; }

    public int SpeedIndex { get; private set; } = DefaultSpeedIndex;

    public double Speed => Speeds[SpeedIndex];

    public bool AtEnd => Playback.Position == Playback.StepCount;

    /// <summary>
    /// Whether a step of <paramref name="kind"/> is given its own time on screen while playing.
    /// </summary>
    /// <remarks>
    /// Planning preparation, notification and Comlink bookkeeping and the moves a load records
    /// change nothing the board draws, so playing them at the same pace as an order or a phase
    /// would show the same picture for most of the run. They are applied together with the next
    /// shown step instead. Stepping by hand still stops on every step.
    /// </remarks>
    public static bool IsShownStep(ReplayOperationKind kind) => kind switch
    {
        ReplayOperationKind.PrepareHireOffers
            or ReplayOperationKind.PrepareSimultaneousHireOffers
            or ReplayOperationKind.PrepareAiPlanning
            or ReplayOperationKind.PrepareAiHiring
            or ReplayOperationKind.DismissNotification
            or ReplayOperationKind.MarkComlinkRead
            or ReplayOperationKind.ContinueRandomStream
            or ReplayOperationKind.EmptyComlinkInboxes
            or ReplayOperationKind.RefreshAiSectorRecords => false,
        _ => true
    };

    /// <summary>Carries out <paramref name="command"/>; <see cref="ReplayViewerCommand.Exit"/> is the caller's.</summary>
    /// <returns>Whether the shown position changed.</returns>
    public bool Execute(ReplayViewerCommand command)
    {
        switch (command)
        {
            case ReplayViewerCommand.TogglePlay:
                if (!Playing && AtEnd) SeekTo(0);
                Playing = !Playing;
                _elapsed = 0;
                return false;
            case ReplayViewerCommand.Faster:
                SpeedIndex = Math.Min(SpeedIndex + 1, Speeds.Count - 1);
                return false;
            case ReplayViewerCommand.Slower:
                SpeedIndex = Math.Max(SpeedIndex - 1, 0);
                return false;
            case ReplayViewerCommand.Start: return SeekTo(0);
            case ReplayViewerCommand.PreviousTurn: return SeekTo(Playback.PreviousTurnStart());
            case ReplayViewerCommand.PreviousStep: return SeekTo(Playback.Position - 1);
            case ReplayViewerCommand.NextStep: return SeekTo(Playback.Position + 1);
            case ReplayViewerCommand.NextTurn: return SeekTo(Playback.NextTurnStart());
            case ReplayViewerCommand.End: return SeekTo(Playback.StepCount);
            default: return false;
        }
    }

    /// <summary>Moves to <paramref name="position"/>, clamped to the journal, and pauses.</summary>
    /// <returns>Whether the shown position changed.</returns>
    public bool SeekTo(int position)
    {
        Playing = false;
        _elapsed = 0;
        var target = Math.Clamp(position, 0, Playback.StepCount);
        if (target == Playback.Position) return false;
        Playback.Seek(target);
        return true;
    }

    /// <summary>Plays on by <paramref name="elapsed"/> of real time; stops at the end.</summary>
    /// <returns>Whether the shown position changed.</returns>
    public bool Advance(TimeSpan elapsed)
    {
        if (!Playing) return false;
        _elapsed += elapsed.TotalSeconds;
        var interval = SecondsPerShownStep / Speed;
        var moved = false;
        var applied = 0;
        while (_elapsed >= interval && applied < MaximumStepsPerUpdate)
        {
            // One shown step per interval, with the bookkeeping steps before it carried along.
            while (Playback.MoveNext())
            {
                moved = true;
                applied++;
                if (IsShownStep(Playback.CurrentStep!.Kind) || applied >= MaximumStepsPerUpdate) break;
            }
            _elapsed -= interval;
            if (AtEnd)
            {
                Playing = false;
                _elapsed = 0;
                break;
            }
        }
        // A frame that hit the cap does not bank the time it could not use.
        if (applied >= MaximumStepsPerUpdate) _elapsed = Math.Min(_elapsed, interval);
        return moved;
    }

    /// <summary>The words the viewer shows for a step, at most <paramref name="width"/> characters.</summary>
    public static string DescribeStep(MatchState state, ReplayStep? step, int width)
    {
        if (step is null) return Fit("OPENING STATE", width);
        var action = step.Kind switch
        {
            ReplayOperationKind.SubmitCommand => "GIVE ORDER",
            ReplayOperationKind.CancelCommand => "CANCEL ORDER",
            ReplayOperationKind.QueueHire => "HIRE GANG",
            ReplayOperationKind.SnubHireOffer => "SNUB OFFER",
            ReplayOperationKind.FinishUpkeep => "END UPKEEP",
            ReplayOperationKind.FinishCommand => "END ORDERS",
            ReplayOperationKind.FinishExecutionPhase => "RESOLVE PHASE",
            ReplayOperationKind.FinishHire => "END HIRING",
            ReplayOperationKind.FinishPlayerElimination => "ELIMINATION",
            ReplayOperationKind.DismissNotification => "READ EVENT",
            ReplayOperationKind.PrepareHireOffers => "DRAW OFFERS",
            ReplayOperationKind.PrepareSimultaneousHireOffers => "DRAW OFFERS",
            ReplayOperationKind.PrepareAiPlanning => "AI PLANNING",
            ReplayOperationKind.PrepareAiHiring => "AI HIRING",
            ReplayOperationKind.SendComlinkMessage => "SEND MESSAGE",
            ReplayOperationKind.MarkComlinkRead => "READ MESSAGE",
            ReplayOperationKind.TransferPlayerToComputer => "SEAT TO COMPUTER",
            ReplayOperationKind.TransferPlayerToHuman => "SEAT TO HUMAN",
            ReplayOperationKind.ContinueRandomStream => "GAME LOADED",
            ReplayOperationKind.EmptyComlinkInboxes => "GAME LOADED",
            ReplayOperationKind.RefreshAiSectorRecords => "GAME LOADED",
            _ => step.Kind.ToString().ToUpperInvariant()
        };
        var playerId = step.Player ?? step.Command?.Player;
        var player = playerId is { } id ? state.FindPlayer(id) : null;
        return Fit(player is null ? action : $"{action} {player.Setup.Name.ToUpperInvariant()}", width);
    }

    /// <summary>The phase line: the turn's phase, and the execution phase while one runs.</summary>
    public static string DescribePhase(MatchState state) =>
        state.Outcome is not null ? "MATCH OVER"
        : state.Coordinator.Phase == TurnPhase.Execution && state.Coordinator.ExecutionPhase is { } execution
            ? $"EXECUTION {execution.ToString().ToUpperInvariant()}"
            : state.Coordinator.Phase switch
            {
                TurnPhase.Command => "ORDERS",
                TurnPhase.PlayerElimination => "ELIMINATION",
                var phase => phase.ToString().ToUpperInvariant()
            };

    public string DescribeTransport() =>
        string.Create(CultureInfo.InvariantCulture,
            $"{(Playing ? "PLAYING" : "PAUSED")} {Speed:0.##}X");

    /// <summary>The status line: the end, or that the journal shown is the backup.</summary>
    public string DescribeStatus() =>
        AtEnd && Playback.StepCount > 0 ? "END OF REPLAY"
        : PrimaryFailure is not null ? "SHOWING BACKUP REPLAY"
        : "VERIFIED REPLAY";

    /// <summary>
    /// The console message for a replay that could not be opened, or for the primary journal when the
    /// viewer shows the backup. Every message fits the city console (<see cref="CityStatusMessage"/>).
    /// </summary>
    public static string DescribeFailure(ReplayFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return CityStatusMessage.RequireFit(failure.Kind switch
        {
            ReplayFailureKind.Missing => "NO REPLAY SAVED  F6 SAVES ONE",
            ReplayFailureKind.Incompatible => failure.Incompatibility switch
            {
                IncompatibleSaveReason.NewerFormat => "REPLAY FROM A NEWER VERSION",
                IncompatibleSaveReason.OlderFormat => "REPLAY FROM AN OLDER VERSION",
                _ => "REPLAY USES OTHER GAME DATA"
            },
            ReplayFailureKind.Diverged => failure.DivergedStep is >= 0 and var step
                ? string.Create(CultureInfo.InvariantCulture, $"REPLAY DIVERGED AT STEP {step + 1}")
                : "REPLAY START DOES NOT MATCH",
            ReplayFailureKind.Damaged => "REPLAY FILE DAMAGED",
            _ => "REPLAY FILE COULD NOT BE READ"
        });
    }

    /// <summary>
    /// The panel's line for why the primary journal is not the one shown. A missing primary is
    /// worded for that case, since the backup on screen shows a replay was saved.
    /// </summary>
    public static string DescribePrimaryFailure(ReplayFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return failure.Kind == ReplayFailureKind.Missing ? "LATEST REPLAY FILE MISSING" : DescribeFailure(failure);
    }

    private static string Fit(string text, int width) => text.Length <= width ? text : text[..width];
}

/// <summary>
/// The replay viewer's panel, drawn over the city console's controls, which do nothing while a
/// replay is shown. The map and the selected sector's values to its left stay visible.
/// </summary>
internal static class ReplayControlLayout
{
    public static readonly Rectangle Panel = new(436, 108, 200, 350);
    public const int TextLeft = 442;
    public const int TextColumns = (200 - 12) / OriginalFontLayout.CellWidth;

    /// <summary>The bar that shows where in the journal the viewer stands; a click seeks.</summary>
    public static readonly Rectangle Timeline = new(442, 186, 188, 10);

    private const int ButtonWidth = 60;
    private const int ButtonHeight = 20;
    private const int ButtonGap = 4;
    private const int ButtonsTop = 206;

    /// <summary>The panel's buttons, three to a row, in reading order.</summary>
    public static IReadOnlyList<(Rectangle Bounds, ReplayViewerCommand Command)> Buttons { get; } =
    [
        (Button(0, 0), ReplayViewerCommand.Start),
        (Button(0, 1), ReplayViewerCommand.PreviousTurn),
        (Button(0, 2), ReplayViewerCommand.PreviousStep),
        (Button(1, 0), ReplayViewerCommand.TogglePlay),
        (Button(1, 1), ReplayViewerCommand.NextStep),
        (Button(1, 2), ReplayViewerCommand.NextTurn),
        (Button(2, 0), ReplayViewerCommand.End),
        (Button(2, 1), ReplayViewerCommand.Slower),
        (Button(2, 2), ReplayViewerCommand.Faster),
        (new Rectangle(TextLeft, ButtonsTop + 3 * (ButtonHeight + ButtonGap), 3 * ButtonWidth + 2 * ButtonGap,
            ButtonHeight), ReplayViewerCommand.Exit)
    ];

    /// <summary>The keys, listed at the foot of the panel.</summary>
    public static IReadOnlyList<string> KeyHelp { get; } =
    [
        "SPACE PLAY  LEFT RIGHT STEP",
        "PGUP PGDN TURN  HOME END",
        "UP DOWN SPEED  WASD SECTOR",
        "ESC OR RIGHT CLICK EXITS"
    ];

    /// <summary>The label of <paramref name="command"/>'s button.</summary>
    public static string Label(ReplayViewerCommand command, bool playing) => command switch
    {
        ReplayViewerCommand.Start => "START",
        ReplayViewerCommand.PreviousTurn => "< TURN",
        ReplayViewerCommand.PreviousStep => "< STEP",
        ReplayViewerCommand.TogglePlay => playing ? "PAUSE" : "PLAY",
        ReplayViewerCommand.NextStep => "STEP >",
        ReplayViewerCommand.NextTurn => "TURN >",
        ReplayViewerCommand.End => "END",
        ReplayViewerCommand.Slower => "SLOWER",
        ReplayViewerCommand.Faster => "FASTER",
        _ => "EXIT REPLAY"
    };

    /// <summary>The button under <paramref name="point"/>, if any.</summary>
    public static ReplayViewerCommand? CommandAt(Point point)
    {
        foreach (var (bounds, command) in Buttons)
            if (bounds.Contains(point)) return command;
        return null;
    }

    /// <summary>The journal position a click at <paramref name="point"/> on the timeline asks for.</summary>
    public static int? TimelinePositionAt(Point point, int stepCount)
    {
        if (!Timeline.Contains(point)) return null;
        var fraction = (point.X - Timeline.Left) / (double)(Timeline.Width - 1);
        return (int)Math.Round(fraction * stepCount, MidpointRounding.AwayFromZero);
    }

    /// <summary>How much of the timeline is filled at <paramref name="position"/>.</summary>
    public static int TimelineFill(int position, int stepCount) =>
        stepCount == 0 ? Timeline.Width : (int)((long)Timeline.Width * position / stepCount);

    private static Rectangle Button(int row, int column) => new(
        TextLeft + column * (ButtonWidth + ButtonGap), ButtonsTop + row * (ButtonHeight + ButtonGap),
        ButtonWidth, ButtonHeight);
}

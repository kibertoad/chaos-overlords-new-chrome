using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;
using Xunit;
using WirePlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

namespace Rechaos.Tests;

/// <summary>
/// The fold of a match's log that a reconnecting client's session and the server's resolver share:
/// seats, handovers at the log's turn, and which seals the state already holds.
/// </summary>
/// <remarks>
/// The session's restore and repair paths are covered through <c>MultiplayerSessionTests</c>, and
/// the resolver's through <see cref="AuthoritativeMatchTests"/>.
/// </remarks>
public sealed class MatchHistoryTests
{
    private static readonly OriginalData Definitions = BundledOriginalData.Load();

    private static readonly IReadOnlyList<PlayerView> Roster =
    [
        new("p1", 0, "ADA", PortraitId: 0, Status: WirePlayerStatus.Active, IsHost: true),
        new("p2", 1, "GRACE", PortraitId: 1, Status: WirePlayerStatus.Active, IsHost: false),
        new("lobby", -1, "LINUS", PortraitId: 2, Status: WirePlayerStatus.Active, IsHost: false),
    ];

    private static MatchHistory NewHistory(int logTurn = 1)
    {
        var settings = new MultiplayerGameSettings(
            ScenarioId.Greed, GameDuration.SixMonths, AiDifficulty.Criminal, [0, 1, 2, 3, 4, 5]);
        var replay = new MatchReplayRecorder(MatchBootstrapFactory.Create(Definitions, 1996, settings, Roster));
        CommandPhase.Enter(replay);
        return new MatchHistory(replay, MatchHistory.SeatsOf(Roster), logTurn);
    }

    private static PlayerController ControllerOf(MatchHistory history, int slot) =>
        history.Replay.State.FindPlayer(new PlayerId(slot))!.Setup.Controller;

    [Fact]
    public void SeatsOnlyThePlayersTheRosterGivesAChair()
    {
        Assert.Equal(new Dictionary<string, int> { ["p1"] = 0, ["p2"] = 1 }, MatchHistory.SeatsOf(Roster));
    }

    [Fact]
    public void AppliesAHandoverAtTheLogsTurnAndPassesOverOneTheStateAlreadyReflects()
    {
        var history = NewHistory();
        history.Apply(new MatchPlayerTakenOverEvent(1, "m", "t", new("p2")));
        Assert.Equal(PlayerController.Computer, ControllerOf(history, 1));

        // A walk whose log is behind the state: the handover is dated before a turn the state has
        // already passed, so it is recorded and not applied again.
        var behind = NewHistory(logTurn: 0);
        behind.Apply(new MatchPlayerTakenOverEvent(1, "m", "t", new("p2")));
        Assert.Equal(PlayerController.Human, ControllerOf(behind, 1));

        // A player the seats do not hold changes nothing.
        history.Apply(new MatchPlayerReturnedEvent(2, "m", "t", new("stranger", true)));
        history.Apply(new MatchPlayerReturnedEvent(3, "m", "t", new("p2", ReplacedComputer: false)));
        Assert.Equal(PlayerController.Computer, ControllerOf(history, 1));
    }

    [Fact]
    public void RecordsHandoversSoARebuildPutsThemBackAtTheirTurns()
    {
        var history = NewHistory();
        history.Apply(new TurnOpenedEvent(1, "m", "t", new(3, null)));
        history.Apply(new MatchPlayerTakenOverEvent(2, "m", "t", new("p2")));
        Assert.Equal(3, history.LogTurn);

        var rebuilt = NewHistory().Replay;
        history.ApplyHandovers(rebuilt, afterTurn: 0, throughTurn: 2);
        Assert.Equal(PlayerController.Human, rebuilt.State.FindPlayer(new PlayerId(1))!.Setup.Controller);
        history.ApplyHandovers(rebuilt, afterTurn: 2, throughTurn: 3);
        Assert.Equal(PlayerController.Computer, rebuilt.State.FindPlayer(new PlayerId(1))!.Setup.Controller);
    }

    [Fact]
    public void SeatsALatePlayerAndRefusesOneThatContradictsTheSeats()
    {
        var history = NewHistory();
        Assert.True(history.AddLatePlayer("late", 2, beforeTurn: 1));
        Assert.Equal(2, history.Seats["late"]);
        Assert.Equal(PlayerController.Human, ControllerOf(history, 2));
        Assert.False(history.AddLatePlayer("late", 2, beforeTurn: 1));

        Assert.Throws<MultiplayerProtocolException>(() => history.AddLatePlayer("late", 3, beforeTurn: 1));
        Assert.Throws<MultiplayerProtocolException>(() => history.AddLatePlayer("other", 1, beforeTurn: 1));
    }

    [Fact]
    public void AsksForTheSetOfASealOnlyWhenTheStateIsOnItsTurn()
    {
        var history = NewHistory();
        var held = history.Apply(new TurnSealedEvent(1, "m", "t", new(0, "digest")));
        var due = history.Apply(new TurnSealedEvent(2, "m", "t", new(1, "digest")));

        Assert.Null(held);
        Assert.Equal(1, due?.Turn);
        Assert.Equal(2, history.LogTurn);
        Assert.Throws<MultiplayerProtocolException>(
            () => history.Apply(new TurnSealedEvent(3, "m", "t", new(2, "digest"))));
        Assert.Null(history.Apply(new TurnReadinessEvent(4, "m", "t", new(1, "p1", true))));
    }
}

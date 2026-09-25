using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// FMT-STATE-006: the `report_type` and arguments the original stores for each kind of Last Turn
/// report, rebuilt from the rebuild's notification and event, and the illustration and subject
/// SCR-EVENT-001 draws from them.
/// </summary>
public sealed class LastTurnReportRecordTests
{
    private const int Turn = 3;
    private const int Sector = 10; // C2

    private static readonly MatchState State = OriginalMatchFactory.Create(
        BundledOriginalData.Load(),
        new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996,
        [
            new MatchPlayerSetup(new PlayerId(0), "PLAYER 1", PlayerController.Human, 0),
            new MatchPlayerSetup(new PlayerId(1), "Rival", PlayerController.Human, 1)
        ]));

    // RULE-EVENT-004, RULE-EVENT-012, RULE-EVENT-013: types 1 to 3 carry the sector in `arg1`,
    // and the panel shows its label even when the notification names a gang.
    [Theory]
    [InlineData(GameNotificationKind.Crackdown, 1)]
    [InlineData(GameNotificationKind.Control, 2)]
    [InlineData(GameNotificationKind.ControlLost, 3)]
    public void SectorReportsShowTheSectorLabel(GameNotificationKind kind, int type)
    {
        var related = kind == GameNotificationKind.Control
            ? Event(GameEventKind.CommandResolved, GangAction.Control, CommandTarget.None,
                new GangId(10), resolution: new CommandResolutionDetails(
                    CommandResolutionCode.Resolved, [], 1, PreviousValue: 1, ResultValue: 0))
            : null;
        var notification = Notification(kind, related, gang: new GangId(10), sector: Sector);

        var record = LastTurnEventPresentation.Record(State, notification, related);

        Assert.Equal(type, record.Type);
        Assert.Equal(Sector, record.Arg1);
        Assert.Equal(kind == GameNotificationKind.Control ? 1 : kind == GameNotificationKind.ControlLost ? -1 : 0,
            record.Arg2);
        Assert.Equal(type, LastTurnEventPresentation.ArtworkIndex(notification, related));
        Assert.Equal("C2", LastTurnEventPresentation.Subject(State, record));
    }

    // RULE-EVENT-007: type 5 carries the item.
    [Fact]
    public void ResearchReportCarriesTheItem()
    {
        var related = Event(GameEventKind.CommandResolved, GangAction.Research, CommandTarget.Item(12),
            new GangId(10));
        var notification = Notification(GameNotificationKind.Research, related, new GangId(10), Sector);

        var record = LastTurnEventPresentation.Record(State, notification, related);

        Assert.Equal(new LastTurnReportRecord(5, 12, 0, 0), record);
        Assert.Equal(State.Definitions.Items[12].Name, LastTurnEventPresentation.Subject(State, record));
    }

    // RULE-EVENT-008: a Bribe short of cash is type 6 with `arg1` 1 and the gang's sector in
    // `arg2`; the subject is the sector label.
    [Fact]
    public void BribeShortOfCashShowsTheGangsSector()
    {
        var related = Event(GameEventKind.CommandFailed, GangAction.Bribe, CommandTarget.None,
            new GangId(10), resolution: new CommandResolutionDetails(
                CommandResolutionCode.InsufficientCash, [], 0));
        var notification = Notification(GameNotificationKind.CommandResult, related, new GangId(10), Sector);

        var record = LastTurnEventPresentation.Record(State, notification, related);

        Assert.Equal(new LastTurnReportRecord(6, 1, Sector, 0), record);
        Assert.Equal(6, LastTurnEventPresentation.ArtworkIndex(notification, related));
        Assert.Equal("C2", LastTurnEventPresentation.Subject(State, record));
    }

    // RULE-EVENT-014: an Equip short of cash is type 6 with `arg1` 2, the gang's sector in `arg2`
    // and its definition in `arg3`; the subject is the label, a colon and the definition name cut
    // to 20 characters.
    [Fact]
    public void EquipShortOfCashShowsTheSectorAndGangName()
    {
        var gang = State.Players[0].Gangs.First(value => value.IsActive);
        var related = Event(GameEventKind.CommandFailed, GangAction.Equip, CommandTarget.Item(12),
            gang.Id, resolution: new CommandResolutionDetails(
                CommandResolutionCode.InsufficientCash, [], 0));
        var notification = Notification(GameNotificationKind.Equipment, related, gang.Id, gang.SectorId);

        var record = LastTurnEventPresentation.Record(State, notification, related);

        Assert.Equal(new LastTurnReportRecord(6, 2, gang.SectorId, gang.DefinitionId), record);
        var name = State.Definitions.Gang(gang.DefinitionId).Name;
        Assert.Equal($"{SectorGangsLayout.SectorCodeText(gang.SectorId)}:{name[..Math.Min(20, name.Length)]}",
            LastTurnEventPresentation.Subject(State, record));
    }

    // RULE-EVENT-009, RULE-EVENT-010, RULE-EVENT-011: a hire short of cash carries the definition
    // in `arg2`, a hire into a full sector the sector in `arg1`, and a hire past the gang limit
    // the definition in `arg1`. Types 7 and 8 draw illustrations 7 and 8.
    [Theory]
    [InlineData(GameNotificationKind.HireInsufficientCash, 6)]
    [InlineData(GameNotificationKind.HireSectorFull, 7)]
    [InlineData(GameNotificationKind.HireGangLimit, 8)]
    public void HireReportsCarryTheirArguments(GameNotificationKind kind, int type)
    {
        const short definition = 7;
        var related = Event(GameEventKind.HireFailed, GangAction.None, CommandTarget.None, null,
            hire: new HireResolutionDetails(definition, Sector, 50));
        var notification = Notification(kind, related, null, Sector);

        var record = LastTurnEventPresentation.Record(State, notification, related);

        var expected = kind switch
        {
            GameNotificationKind.HireInsufficientCash => new LastTurnReportRecord(6, 4, definition, 0),
            GameNotificationKind.HireSectorFull => new LastTurnReportRecord(7, Sector, 0, 0),
            _ => new LastTurnReportRecord(8, definition, 0, 0)
        };
        Assert.Equal(expected, record);
        Assert.Equal(type, LastTurnEventPresentation.ArtworkIndex(notification, related));
        Assert.Equal(kind == GameNotificationKind.HireSectorFull
                ? "C2"
                : State.Definitions.Gang(definition).Name,
            LastTurnEventPresentation.Subject(State, record));
    }

    // RULE-EVENT-003: type 9 carries the eliminated player; the subject is its name.
    [Fact]
    public void EliminationReportCarriesThePlayer()
    {
        var related = Event(GameEventKind.PlayerEliminated, GangAction.None, CommandTarget.None, null,
            elimination: new EliminationDetails(new PlayerId(1), 1));
        var notification = Notification(GameNotificationKind.Elimination, related, null, null);

        var record = LastTurnEventPresentation.Record(State, notification, related);

        Assert.Equal(new LastTurnReportRecord(9, 1, 0, 0), record);
        Assert.Equal(9, LastTurnEventPresentation.ArtworkIndex(notification, related));
        Assert.Equal("RIVAL", LastTurnEventPresentation.Subject(State, record));
    }

    private static GameEvent Event(
        GameEventKind kind,
        GangAction action,
        CommandTarget target,
        GangId? gang,
        CommandResolutionDetails? resolution = null,
        HireResolutionDetails? hire = null,
        EliminationDetails? elimination = null) =>
        new(40, Turn, TurnPhase.Execution, ExecutionPhase.Instant, kind, new PlayerId(0), gang,
            action, target, Resolution: resolution, Hire: hire, Elimination: elimination);

    private static GameNotification Notification(
        GameNotificationKind kind,
        GameEvent? related,
        GangId? gang,
        int? sector) =>
        new(41, Turn, TurnPhase.Execution, ExecutionPhase.Instant, kind, gang, sector,
            related?.Sequence);
}

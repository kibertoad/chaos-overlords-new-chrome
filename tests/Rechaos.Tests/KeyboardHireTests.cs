using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// DEV-UI-028: the keys 1 to 3 hire the offer in that dock slot into the selected sector, with the
/// order, refusals and flash a drag of the same offer onto the same sector gives (SCR-HIRE-002,
/// RULE-HIRE-003, HireDropPlacement for the six-gang limit).
/// </summary>
public sealed class KeyboardHireTests
{
    [Theory]
    [InlineData(0, Keys.D1)]
    [InlineData(1, Keys.D2)]
    [InlineData(2, Keys.D3)]
    public void AKeyOnTheCityQueuesTheHireADragOntoTheSelectedSectorQueues(int slot, Keys key)
    {
        var byKey = Planning(ClientScreen.City);
        var byDrag = Planning(ClientScreen.City);
        var sector = HomeSector(byKey.State);

        Select(byKey.Game, sector);
        DeviationBehaviourTests.Call(byKey.Game, "UpdateCity", new KeyboardState(key));
        Drag(byDrag.Game, slot, CityPoint(sector));

        var pending = Assert.Single(byKey.State.Players[0].PendingHires);
        Assert.Equal(sector, pending.TargetSectorId);
        Assert.Equal(byKey.State.Players[0].HireOfferSlots[slot].GangDefinitionId, pending.GangDefinitionId);
        AssertSameOutcome(byDrag, byKey);
        Assert.Equal(TickedPresentationKind.CityCellFlash, FlashKind(byKey.Game));
    }

    [Fact]
    public void AKeyOnTheSectorViewQueuesTheHireADropOnItsWorkspaceQueues()
    {
        var byKey = Planning(ClientScreen.Sector);
        var byDrag = Planning(ClientScreen.Sector);
        var sector = HomeSector(byKey.State);
        Select(byKey.Game, sector);
        Select(byDrag.Game, sector);

        DeviationBehaviourTests.Call(byKey.Game, "UpdateSector", new KeyboardState(Keys.D2));
        Drag(byDrag.Game, 1, SectorDetailLayout.Workspace.Center);

        Assert.Single(byKey.State.Players[0].PendingHires);
        AssertSameOutcome(byDrag, byKey);
        Assert.Equal(TickedPresentationKind.SectorDisplayCellFlash, FlashKind(byKey.Game));
    }

    [Fact]
    public void AKeyOnASectorTheRulesRefuseIsRefusedWithTheDragsMessage()
    {
        var byKey = Planning(ClientScreen.City);
        var byDrag = Planning(ClientScreen.City);
        var sector = byKey.State.Sectors.First(candidate => candidate.Owner is null).Id;

        Select(byKey.Game, sector);
        DeviationBehaviourTests.Call(byKey.Game, "UpdateCity", new KeyboardState(Keys.D1));
        Drag(byDrag.Game, 0, CityPoint(sector));

        Assert.Empty(byKey.State.Players[0].PendingHires);
        Assert.NotEqual(string.Empty, Message(byKey.Game));
        AssertSameOutcome(byDrag, byKey);
    }

    [Fact]
    public void AKeyOutsidePlanningIsRefusedAsAPressOnTheDockIs()
    {
        var byKey = Planning(ClientScreen.City);
        var byPress = Planning(ClientScreen.City);
        DeviationBehaviourTests.Field("_actions").SetValue(byKey.Game, null);
        DeviationBehaviourTests.Field("_actions").SetValue(byPress.Game, null);

        DeviationBehaviourTests.Call(byKey.Game, "UpdateCity", new KeyboardState(Keys.D1));
        DeviationBehaviourTests.Call(byPress.Game, "BeginHireDrag", 0, HireDockLayout.Portrait(0).Center);

        Assert.Equal("HIRING REQUIRES A PLANNING TURN", Message(byKey.Game));
        Assert.Equal(Message(byPress.Game), Message(byKey.Game));
        Assert.Empty(byKey.State.Players[0].PendingHires);
    }

    private sealed record PlanningGame(ChaosGame Game, MatchState State, MatchActions Actions);

    private static PlanningGame Planning(ClientScreen screen)
    {
        MatchPlayerSetup[] players =
        [
            new(new PlayerId(0), "ONE", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Human)
        ];
        var state = OriginalMatchFactory.Create(BundledOriginalData.Load(),
            new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, players));
        var replay = new MatchReplayRecorder(state);
        GameplayTurnFlow.AdvanceToPlanning(replay);
        Assert.Equal(TurnPhase.Command, state.Coordinator.Phase);
        var actions = new MatchActions(replay);
        var game = DeviationBehaviourTests.HeadlessGame();
        DeviationBehaviourTests.Field("_state").SetValue(game, state);
        DeviationBehaviourTests.Field("_actions").SetValue(game, actions);
        ((ScreenRouter)DeviationBehaviourTests.Field("_screens").GetValue(game)!).Show(screen);
        return new PlanningGame(game, state, actions);
    }

    private static int HomeSector(MatchState state) => state.Players[0].Gangs[0].SectorId;

    private static void Select(ChaosGame game, int sector) =>
        DeviationBehaviourTests.Field("_cursor").SetValue(game, sector);

    private static Point CityPoint(int sector) => new(
        CityMapLayout.Left + sector % MatchLimits.BoardWidth * CityMapLayout.TileWidth + CityMapLayout.TileWidth / 2,
        CityMapLayout.Top + sector / MatchLimits.BoardWidth * CityMapLayout.TileHeight + CityMapLayout.TileHeight / 2);

    private static void Drag(ChaosGame game, int slot, Point drop)
    {
        DeviationBehaviourTests.Call(game, "BeginHireDrag", slot, HireDockLayout.Portrait(slot).Center);
        DeviationBehaviourTests.Call(game, "CompleteHireDrag", drop);
    }

    private static void AssertSameOutcome(PlanningGame expected, PlanningGame actual)
    {
        Assert.Equal(MatchStateHasher.ComputeFingerprint(expected.State),
            MatchStateHasher.ComputeFingerprint(actual.State));
        Assert.Equal(
            expected.Actions.Journal.Steps.Select(step => (step.Kind, step.ResultingStateFingerprint)),
            actual.Actions.Journal.Steps.Select(step => (step.Kind, step.ResultingStateFingerprint)));
        Assert.Equal(Message(expected.Game), Message(actual.Game));
        Assert.Equal(FlashKind(expected.Game), FlashKind(actual.Game));
        Assert.Equal(Flash(expected.Game).Area, Flash(actual.Game).Area);
    }

    private static string Message(ChaosGame game) =>
        (string?)DeviationBehaviourTests.Field("_message").GetValue(game) ?? string.Empty;

    private static TickedPresentation Flash(ChaosGame game) =>
        (TickedPresentation)DeviationBehaviourTests.Field("_tickedPresentation").GetValue(game)!;

    private static TickedPresentationKind? FlashKind(ChaosGame game) =>
        Flash(game) is { Active: true } flash ? flash.Kind : null;
}

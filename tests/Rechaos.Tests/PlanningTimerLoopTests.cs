using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

// RULE-TIMER-002: the original tests the time limit only on a pass of the planning loop, so a
// panel open, the idle-gang warning up, or an offer, a control or a gang held at the limit keeps the
// turn going until it is closed or let go, and the first pass after that ends the turn. Each test
// plays a local match in a HeadlessGame with a 30-second limit and drives it by its keys and
// pointer only.
public sealed class PlanningTimerLoopTests
{
    private static readonly TimeSpan PastTheLimit = TimeSpan.FromSeconds(31);

    [Fact]
    public void TheFirstPassPastTheLimitEndsTheTurn()
    {
        using var game = TimedGame(out var human);
        game.Advance(TimeSpan.FromSeconds(29));
        AssertPlanning(game, human);

        game.Jump(TimeSpan.FromSeconds(2));
        AssertTurnEnded(game, human);
        Assert.False(game.Game.PlanningClock.IsActive);
    }

    [Fact]
    public void AnOpenPanelDefersTheExpiryUntilItCloses()
    {
        using var game = TimedGame(out var human);
        game.Press(Keys.C);
        Assert.Equal(ClientScreen.Commands, game.Game.CurrentScreen);
        game.Jump(PastTheLimit);
        game.Advance(TimeSpan.FromSeconds(1));
        AssertPlanning(game, human);
        Assert.True(game.Game.PlanningClock.HasExpired(game.Now));

        // The test runs before the frame's input, so the update that closes the panel is not yet a
        // pass of the planning loop; the next one is.
        game.HoldKey(Keys.Back);
        Assert.Equal(ClientScreen.City, game.Game.CurrentScreen);
        AssertPlanning(game, human);
        game.ReleaseKey(Keys.Back);
        AssertTurnEnded(game, human);
    }

    [Fact]
    public void TheSectorViewIsAPassOfThePlanningLoop()
    {
        using var game = TimedGame(out var human);
        game.Press(Keys.I);
        Assert.Equal(ClientScreen.Sector, game.Game.CurrentScreen);
        game.Jump(PastTheLimit);
        AssertTurnEnded(game, human);
    }

    [Fact]
    public void TheIdleGangWarningDefersTheExpiryUntilItIsCancelled()
    {
        // SCR-OPTIONS-001: Done with a gang that has no order opens the warning, which answers
        // before the planning loop tests the limit.
        using var game = TimedGame(out var human, warnIfIdleGangs: true);
        game.Press(Keys.Space);
        Assert.True(game.Game.IdleGangWarningOpen);
        game.Jump(PastTheLimit);
        game.Advance(TimeSpan.FromSeconds(1));
        AssertPlanning(game, human);
        Assert.True(game.Game.IdleGangWarningOpen);

        // FND-UI-024: Escape presses Cancel for one tick of the presentation clock, then closes.
        game.HoldKey(Keys.Escape);
        game.ReleaseKey(Keys.Escape);
        while (game.Game.IdleGangWarningOpen && game.Now < TimeSpan.FromSeconds(40))
        {
            AssertPlanning(game, human);
            game.Tick();
        }
        Assert.False(game.Game.IdleGangWarningOpen);
        AssertPlanning(game, human);
        game.Tick();
        AssertTurnEnded(game, human);
    }

    [Fact]
    public void AHeldConsoleTileDefersTheExpiryUntilItIsLetGo()
    {
        // FND-UI-032: the console tile helper holds the press in a loop of its own. The release
        // lands outside the tile, so it does not end the turn by itself.
        using var game = TimedGame(out var human);
        game.PressLeft(CityConsoleLayout.Done.Center);
        Assert.True(game.Game.HoldsPlanningLoop);
        game.Jump(PastTheLimit);
        game.MoveTo(new Point(300, 200));
        AssertPlanning(game, human);

        game.ReleaseLeft();
        AssertPlanning(game, human);
        game.Tick();
        AssertTurnEnded(game, human);
    }

    [Fact]
    public void AHeldHireOfferDefersTheExpiryUntilItIsLetGo()
    {
        // FND-HIRE-008: the Hire handler follows a pressed offer until the button comes up.
        using var game = TimedGame(out var human);
        game.PressLeft(HireDockLayout.Portrait(0).Center);
        Assert.True(game.Game.HoldsPlanningLoop);
        game.Jump(PastTheLimit);
        game.Advance(TimeSpan.FromSeconds(1));
        AssertPlanning(game, human);

        game.ReleaseLeft();
        AssertPlanning(game, human);
        game.Tick();
        AssertTurnEnded(game, human);
    }

    [Fact]
    public void AHeldRejectCrossDefersTheExpiryUntilItIsLetGo()
    {
        // FND-HIRE-008: the reject cross is held in a loop of its own as well.
        using var game = TimedGame(out var human);
        game.PressLeft(HireDockLayout.Reject(0).Center);
        Assert.True(game.Game.HoldsPlanningLoop);
        game.Jump(PastTheLimit);
        game.MoveTo(new Point(300, 200));
        AssertPlanning(game, human);

        game.ReleaseLeft();
        AssertPlanning(game, human);
        game.Tick();
        AssertTurnEnded(game, human);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AGangHeldOnTheSectorViewDefersTheExpiryUntilItIsLetGo(bool dragged)
    {
        // FND-UI-044: a left press on the portrait of one of the player's cards waits in the
        // individual command handler for the pointer to leave the rectangle around the press or the
        // button to come up, then follows the dragged gang until the button comes up, pumping
        // window messages only. The planning loop's expiry test runs again after the handler returns.
        using var game = TimedGame(out var human);
        game.Press(Keys.I);
        var portrait = SectorGangCardLayout.Portrait(0);
        game.PressLeft(portrait.Center);
        Assert.NotNull(game.Game.HeldGang);
        game.Jump(PastTheLimit);
        AssertPlanning(game, human);
        // Moved past the press point: the drag loop.
        if (dragged) game.MoveTo(new Point(portrait.Center.X, portrait.Bottom + 40));
        game.Advance(TimeSpan.FromSeconds(1));
        AssertPlanning(game, human);

        game.ReleaseLeft();
        AssertPlanning(game, human);
        game.Tick();
        AssertTurnEnded(game, human);
    }

    [Fact]
    public void AGangHoldLetGoByACancelDefersTheExpiryUntilTheButtonComesUp()
    {
        // FND-UI-044: the original's hold loops end only when the left button comes up, so the
        // rebuild's right press, which drops the held gang, leaves the expiry test waiting.
        using var game = TimedGame(out var human);
        game.Press(Keys.I);
        game.PressLeft(SectorGangCardLayout.Portrait(0).Center);
        Assert.NotNull(game.Game.HeldGang);
        game.PressRight(SectorGangCardLayout.Portrait(0).Center);
        game.ReleaseRight();
        Assert.Null(game.Game.HeldGang);
        game.Jump(PastTheLimit);
        game.Advance(TimeSpan.FromSeconds(1));
        AssertPlanning(game, human);

        game.ReleaseLeft();
        AssertPlanning(game, human);
        game.Tick();
        AssertTurnEnded(game, human);
    }

    /// <summary>
    /// A local match on its first planning turn, with the 30-second limit the preferences hold and
    /// the idle-gang warning as asked; the planning clock started on the update that began it.
    /// </summary>
    private static HeadlessGame TimedGame(out PlayerId human, bool warnIfIdleGangs = false)
    {
        var game = new HeadlessGame(HeadlessGame.DefaultPreferences with
        {
            PlanningTimeLimit = PlanningTimeLimit.ThirtySeconds,
            WarnIfIdleGangs = warnIfIdleGangs,
        });
        try
        {
            var match = game.StartLocalMatch();
            Assert.Equal(ClientScreen.City, game.Game.CurrentScreen);
            human = match.Coordinator.ActivePlayer ?? throw new InvalidOperationException("No player plans.");
            Assert.Equal(PlayerController.Human, match.FindPlayer(human)!.Setup.Controller);
            Assert.True(game.Game.PlanningClock.IsActive);
            return game;
        }
        catch
        {
            game.Dispose();
            throw;
        }
    }

    private static void AssertPlanning(HeadlessGame game, PlayerId human)
    {
        var match = game.Game.Match!;
        Assert.Equal(1, match.Coordinator.Turn);
        Assert.Equal(TurnPhase.Command, match.Coordinator.Phase);
        Assert.Equal(human, match.Coordinator.ActivePlayer);
        Assert.True(game.Game.PlanningClock.IsActive);
    }

    private static void AssertTurnEnded(HeadlessGame game, PlayerId human)
    {
        var match = game.Game.Match!;
        Assert.True(match.Coordinator.Turn > 1 || match.Coordinator.ActivePlayer != human,
            $"turn {match.Coordinator.Turn} is still planned by player {human.Value}");
    }
}

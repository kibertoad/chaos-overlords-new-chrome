using System.Net;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Rechaos.Multiplayer.Generated;
using Xunit;

namespace Rechaos.Tests;

// RULE-AUDIO-006: the original plays the turn-start cue, slot 9, only in a network game, from a
// direct call that skips the effects-enabled test (BUG-AUDIO-001). The rebuild's local turn change
// plays no cue, and its online client plays slot 9 when a sealed turn resolves, with the effects
// switched off as well. Each test plays the game in a HeadlessGame and listens to every effect it
// asks for.
public sealed class TurnStartCueTests
{
    [Theory]
    [InlineData(AudioRouting.DefaultEffectVolumeLevel)]
    [InlineData(0)]
    public void ALocalTurnChangePlaysNoTurnStartCue(int effectsLevel)
    {
        using var game = new HeadlessGame(HeadlessGame.DefaultPreferences with
        {
            SoundEffectVolumeLevel = effectsLevel,
            WarnIfIdleGangs = false,
        });
        var match = game.StartLocalMatch();
        var human = match.Coordinator.ActivePlayer!.Value;
        game.Sounds.Clear();

        // Done, then the computers' turns and the resolution, to the human's next planning entry.
        game.Press(Keys.Space);
        for (var tick = 0; tick < 600 && !(match.Coordinator.Turn == 2
            && match.Coordinator.ActivePlayer == human); tick++)
            game.Tick();
        Assert.Equal(2, match.Coordinator.Turn);
        Assert.Equal(human, match.Coordinator.ActivePlayer);

        Assert.DoesNotContain(GeneralSoundSlot.TurnStartCue, game.Sounds.GeneralSlots);
        if (effectsLevel == 0) Assert.Empty(game.Sounds.Played);
    }

    [Theory]
    [InlineData(AudioRouting.DefaultEffectVolumeLevel)]
    [InlineData(0)]
    public void AnOnlineTurnResolutionPlaysTheTurnStartCueWhateverTheEffectsLevel(int effectsLevel)
    {
        using var server = new FakeMultiplayerServer();
        using var game = JoinedOnlineGame(server, effectsLevel);
        game.Sounds.Clear();

        server.Answer(HttpMethod.Get, "/turns/1/orders", MultiplayerSessionTests.SealedOrders(1));
        server.Events.Write(MultiplayerSessionTests.SealedFrame(seq: 8, turn: 1));
        game.TickUntil(() => game.Game.Match?.Coordinator.Turn == 2, "turn 1 resolving");

        Assert.Equal(ClientScreen.Handoff, game.Game.CurrentScreen);
        Assert.Equal(
            [new PlayedSound(SoundEffectCue.General(GeneralSoundSlot.TurnStartCue),
                AudioRouting.EffectVolumeForLevel(effectsLevel))],
            game.Sounds.Played.Where(sound => sound.Cue == SoundEffectCue.General(GeneralSoundSlot.TurnStartCue)));
        // With the effects switched off the cue is the only effect played (BUG-AUDIO-001), at
        // level 0's volume (RULE-AUDIO-003).
        if (effectsLevel == 0)
            Assert.Equal([SoundEffectCue.General(GeneralSoundSlot.TurnStartCue)],
                game.Sounds.Played.Select(sound => sound.Cue));
    }

    /// <summary>
    /// A game that has joined the session tests' two-seat match by its code, as the second seat,
    /// from the title screen's Online button, and stands on its city at turn 1.
    /// </summary>
    private static HeadlessGame JoinedOnlineGame(FakeMultiplayerServer server, int effectsLevel)
    {
        // The code seats the player in the lobby, and the lobby's next poll finds the host has
        // started the match.
        var view = MultiplayerSessionTests.View();
        var seat = view.Players[1];
        server.Answer(HttpMethod.Post, "/matches/join", new MembershipView(
            view with { Status = MatchStatus.Lobby, CurrentTurn = 0, Turn = null }, seat, "cop_test", "CODE1234"));
        server.Answer(HttpMethod.Get, $"/matches/{view.Id}", new MatchDetail(view, "CODE1234", seat.Id));
        server.Answer(HttpMethod.Post, "/report", null, HttpStatusCode.NoContent);
        server.Answer(HttpMethod.Post, "/snapshots", null, HttpStatusCode.NoContent);
        server.Answer(HttpMethod.Put, "/orders", new OwnSubmissionView(1, null, Ready: true, null));
        var game = new HeadlessGame(
            HeadlessGame.DefaultPreferences with
            {
                SoundEffectVolumeLevel = effectsLevel,
                OnlineService = OnlineServiceMode.Custom,
                CustomMultiplayerServer = "http://server.test",
            },
            multiplayerTransport: server);
        try
        {
            game.Click(new Microsoft.Xna.Framework.Point(370, 350));
            Assert.Equal(ClientScreen.Online, game.Game.CurrentScreen);
            game.Click(OnlineConnectLayout.JoinRole.Center);
            game.Type(seat.DisplayName);
            game.Press(Keys.Tab);
            game.Type("CODE1234");
            game.Press(Keys.Enter);
            game.TickUntil(() => game.Game.CurrentScreen == ClientScreen.City,
                "the joined match opening on the city", onTimeout: () =>
                    $"{game.Game.CurrentScreen} {game.Game.OnlineStatus}; requests: "
                    + string.Join(", ", server.Requests.Select(request => $"{request.Method} {request.Path}")));
            Assert.Equal(1, game.Game.Match!.Coordinator.Turn);
            Assert.Equal(1, server.CallsTo(HttpMethod.Post, "/matches/join"));
            return game;
        }
        catch
        {
            game.Dispose();
            throw;
        }
    }
}

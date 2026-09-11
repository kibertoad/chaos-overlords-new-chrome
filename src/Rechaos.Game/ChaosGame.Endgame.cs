using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private static readonly Rectangle EndgameDone = new(320, 404, 104, 54);
    private Texture2D? _endgameBackground;
    private Texture2D? _victoryBackground;
    private Texture2D? _eliminationBackground;
    private bool _showEndgameNotice;

    private void DrawEndgame(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        if (_cityBackground is not null)
            batch.Draw(_cityBackground, new Rectangle(0, 0, 640, 460), Color.White);
        if (_showEndgameNotice && EndgameNoticePresentation.For(state) is { } notice)
        {
            var background = notice.Kind == EndgameNoticeKind.Victory
                ? _victoryBackground
                : _eliminationBackground;
            if (background is not null)
                batch.Draw(background, EndgameNoticeLayout.Panel, Color.White);
            else
                batch.Draw(pixel, EndgameNoticeLayout.Panel, Color.Black);
            if (_uiSprites is not null)
                batch.Draw(_uiSprites, EndgameNoticeLayout.Portrait,
                    OriginalSpriteLayout.OverlordPortrait(notice.PortraitId), Color.White);
            DrawBorder(batch, pixel, EndgameNoticeLayout.Portrait,
                PlayerColors[notice.Player.Value], 1);
            font.Draw(batch, "PRESS ENTER OR CLICK TO CONTINUE",
                new Vector2(66, 438), Color.White, 1);
            return;
        }
        if (_endgameBackground is not null)
            batch.Draw(_endgameBackground, new Rectangle(0, 50, 428, 410), Color.White);
        else
            batch.Draw(pixel, new Rectangle(0, 50, 428, 410), new Color(0, 0, 0, 230));

        var outcome = state.Outcome!;
        font.Draw(batch, "MATCH COMPLETE", new Vector2(164, 62), Color.Gold, 2);
        font.Draw(batch, ScenarioCatalog.Get(outcome.Scenario).Name,
            new Vector2(164, 86), Color.White, 1);
        font.Draw(batch, $"TURN {outcome.Turn}  {outcome.Reason}",
            new Vector2(164, 101), Color.White, 1);

        var rows = outcome.Standings.Count > 0
            ? outcome.Standings.Select(standing => (
                standing.Player,
                Label: $"{standing.Place}. {state.FindPlayer(standing.Player)!.Setup.Name}",
                Value: standing.Score.ToString())).ToArray()
            : state.Players.OrderByDescending(player => outcome.Winners.Contains(player.Id))
                .ThenBy(player => player.Id.Value)
                .Select(player => (
                    Player: player.Id,
                    Label: player.Setup.Name,
                    Value: outcome.Winners.Contains(player.Id) ? "WINNER" : ""))
                .ToArray();
        for (var index = 0; index < rows.Length; index++)
        {
            var y = 78 + index * 48;
            font.Draw(batch, rows[index].Label, new Vector2(8, y),
                PlayerColors[rows[index].Player.Value], 1);
            font.Draw(batch, rows[index].Value, new Vector2(164, y), Color.White, 1);
        }

        font.Draw(batch, "AWARDS", new Vector2(164, 255), Color.Gold, 1);
        for (var index = 0; index < outcome.Awards.Count; index++)
        {
            var award = outcome.Awards[index];
            var recipients = string.Join(",",
                award.Recipients.Select(player => (player.Value + 1).ToString()));
            font.Draw(batch, $"{award.Award} {award.Value} P{recipients}",
                new Vector2(164, 272 + index * 14), Color.White, 1);
        }
        DrawBorder(batch, pixel, EndgameDone, Color.Gold, 2);
    }

    private void AdvanceEndgamePresentation()
    {
        if (_showEndgameNotice)
            _showEndgameNotice = false;
        else
            _screens.Show(ClientScreen.Title);
    }
}

public enum EndgameNoticeKind
{
    Victory,
    Elimination
}

public sealed record EndgameNotice(EndgameNoticeKind Kind, PlayerId Player, short PortraitId);

public static class EndgameNoticePresentation
{
    public static EndgameNotice? For(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Outcome is null) return null;
        var humans = state.Setup.Players
            .Where(player => player.Controller == PlayerController.Human).ToArray();
        if (humans.Length != 1) return null;
        var human = humans[0];
        return new EndgameNotice(
            state.Outcome.Winners.Contains(human.Id)
                ? EndgameNoticeKind.Victory
                : EndgameNoticeKind.Elimination,
            human.Id,
            human.PortraitId);
    }
}

public static class EndgameNoticeLayout
{
    public static Rectangle Panel => new(0, 67, 312, 393);
    public static Rectangle Portrait => new(15, 80, 64, 76);
}

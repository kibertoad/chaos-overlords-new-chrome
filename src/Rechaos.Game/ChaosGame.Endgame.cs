using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private static readonly Rectangle EndgameDone = EndgameLayout.Done;
    private Texture2D? _endgameBackground;
    private Texture2D? _endgameSprites;
    private Texture2D? _victoryBackground;
    private Texture2D? _eliminationBackground;
    private bool _showEndgameNotice;
    private bool _showEndgameStats;

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
        var rows = EndgamePresentation.Rows(state);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var player = state.FindPlayer(row.Player)!;
            if (_uiSprites is not null)
                batch.Draw(_uiSprites, EndgameLayout.Portrait(index),
                    OriginalSpriteLayout.OverlordPortrait(player.Setup.PortraitId), Color.White);
            font.Draw(batch, row.Label, EndgameLayout.Name(index),
                PlayerColors[row.Player.Value], 1);
            if (_showEndgameStats)
                DrawEndgameStatistics(batch, font, player, index);
            else
                DrawEndgameAwards(batch, font, outcome, row.Player, index);
        }
        DrawBorder(batch, pixel, _showEndgameStats ? EndgameLayout.Stats : EndgameLayout.Awards,
            Color.Gold, 2);
    }

    private void DrawEndgameAwards(
        SpriteBatch batch,
        PixelFont font,
        MatchOutcome outcome,
        PlayerId player,
        int row)
    {
        var awards = outcome.Awards.Where(award => award.Recipients.Contains(player)).ToArray();
        for (var index = 0; index < awards.Length; index++)
        {
            var destination = EndgameLayout.Award(row, index);
            if (_endgameSprites is not null)
                batch.Draw(_endgameSprites, destination,
                    EndgameLayout.AwardSource(awards[index].Award), Color.White);
            else
                font.Draw(batch, EndgamePresentation.AwardAbbreviation(awards[index].Award),
                    new Vector2(destination.X, destination.Y + 10), Color.Gold, 1);
        }
    }

    private static void DrawEndgameStatistics(
        SpriteBatch batch,
        PixelFont font,
        MatchPlayerState player,
        int row)
    {
        (string Label, long Value)[] statistics =
        [
            ("CASH EARNED", player.Statistics.CashEarned),
            ("CASH SPENT", player.Statistics.CashSpent),
            ("DAMAGE", player.Statistics.DamageInflicted),
            ("CASUALTIES", player.Statistics.Casualties),
            ("OVERTHROWS", player.Statistics.Overthrows)
        ];
        for (var index = 0; index < statistics.Length; index++)
        {
            var position = EndgameLayout.Statistic(row, index);
            font.Draw(batch, statistics[index].Label, position, Color.Lime, 1);
            DrawPanelValue(font, batch, statistics[index].Value.ToString(),
                EndgameLayout.StatisticValueRight, (int)position.Y);
        }
    }

    private void AdvanceEndgamePresentation()
    {
        if (_showEndgameNotice)
        {
            var notice = _state is null ? null : EndgameNoticePresentation.For(_state);
            if (notice is not null && EndgameNoticePresentation.ContinuesToSummary(notice.Kind))
                _showEndgameNotice = false;
            else
                _screens.Show(ClientScreen.Title);
        }
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
    public static bool ContinuesToSummary(EndgameNoticeKind kind) =>
        kind == EndgameNoticeKind.Victory;

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

public sealed record EndgamePlayerRow(PlayerId Player, string Label);

public static class EndgamePresentation
{
    public static IReadOnlyList<EndgamePlayerRow> Rows(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var outcome = state.Outcome ?? throw new ArgumentException("Match has not ended.", nameof(state));
        return outcome.Standings.Count > 0
            ? outcome.Standings.Select(standing => new EndgamePlayerRow(
                standing.Player, standing.Place > 0
                    ? $"{standing.Place}. {state.FindPlayer(standing.Player)!.Setup.Name}"
                    : state.FindPlayer(standing.Player)!.Setup.Name))
                .ToArray()
            : state.Players.OrderByDescending(player => outcome.Winners.Contains(player.Id))
                .ThenBy(player => player.Id.Value)
                .Select(player => new EndgamePlayerRow(player.Id,
                    outcome.Winners.Contains(player.Id)
                        ? $"1. {player.Setup.Name}"
                        : player.Setup.Name))
                .ToArray();
    }

    public static string AwardAbbreviation(EndgameAward award) => award switch
    {
        EndgameAward.Skull => "SKULL",
        EndgameAward.Fist => "FIST",
        EndgameAward.DollarSign => "$",
        EndgameAward.Safe => "SAFE",
        EndgameAward.BigFatChicken => "CHICKEN",
        _ => throw new ArgumentOutOfRangeException(nameof(award))
    };
}

public static class EndgameLayout
{
    public const int PlayerRows = MatchLimits.PlayerCount;
    public const int StatisticValueRight = 314;
    public static Rectangle Panel => new(0, 50, 428, 410);
    public static Rectangle Awards => new(320, 50, 50, 56);
    public static Rectangle Stats => new(372, 50, 52, 56);
    public static Rectangle Done => new(320, 402, 104, 58);

    public static Rectangle Portrait(int row)
    {
        ValidateRow(row);
        return new Rectangle(4, 52 + row * 66, 64, 64);
    }

    public static Vector2 Name(int row)
    {
        ValidateRow(row);
        return new Vector2(72, 58 + row * 66);
    }

    public static Rectangle Award(int row, int index)
    {
        ValidateRow(row);
        if (index is < 0 or >= 5) throw new ArgumentOutOfRangeException(nameof(index));
        return new Rectangle(158 + index * 31, 69 + row * 66, 29, 28);
    }

    public static Rectangle AwardSource(EndgameAward award) => award switch
    {
        EndgameAward.Fist => new Rectangle(0, 0, 50, 48),
        EndgameAward.Skull => new Rectangle(50, 0, 50, 48),
        EndgameAward.BigFatChicken => new Rectangle(100, 0, 50, 48),
        EndgameAward.DollarSign => new Rectangle(150, 0, 50, 48),
        EndgameAward.Safe => new Rectangle(200, 0, 50, 48),
        _ => throw new ArgumentOutOfRangeException(nameof(award))
    };

    public static Vector2 Statistic(int row, int statistic)
    {
        ValidateRow(row);
        if (statistic is < 0 or >= 5) throw new ArgumentOutOfRangeException(nameof(statistic));
        return new Vector2(158, 57 + row * 66 + statistic * 11);
    }

    private static void ValidateRow(int row)
    {
        if (row is < 0 or >= PlayerRows) throw new ArgumentOutOfRangeException(nameof(row));
    }
}

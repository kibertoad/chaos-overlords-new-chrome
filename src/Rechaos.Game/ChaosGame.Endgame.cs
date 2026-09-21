using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void HandleEndgameClick(Point point)
    {
        if (_showEndgameNotice && EndgameDone.Contains(point))
        {
            PlayGeneralSound(AudioRouting.PointerPushSound());
            AdvanceEndgamePresentation();
        }
        else if (!_showEndgameNotice && EndgameLayout.Awards.Contains(point))
        {
            PlayGeneralSound(AudioRouting.PointerPushSound());
            _showEndgameStats = false;
        }
        else if (!_showEndgameNotice && EndgameLayout.Stats.Contains(point))
        {
            PlayGeneralSound(AudioRouting.PointerPushSound());
            _showEndgameStats = true;
        }
        else if (!_showEndgameNotice && EndgameDone.Contains(point))
        {
            PlayGeneralSound(AudioRouting.PointerPushSound());
            LeaveEndgame();
        }
    }

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
        DrawEndgameBackground(batch, pixel);
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
            DrawEndgameNoticeName(batch, font, notice.Player, state.FindPlayer(notice.Player)!.Setup.Name);
            return;
        }

        var outcome = state.Outcome!;
        var rows = EndgamePresentation.Rows(state);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var player = state.FindPlayer(row.Player)!;
            if (_endgameSprites is not null && row.Place > 0)
                batch.Draw(_endgameSprites, EndgameLayout.PlayerMarker(index),
                    EndgameLayout.PlayerMarkerSource(player.Id, row.Place), Color.White);
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
        DrawSelectionLight(batch, pixel, OriginalSelectionLightLayout.EndgameTab(
            _showEndgameStats ? EndgameLayout.Stats : EndgameLayout.Awards));
    }

    /// <summary>
    /// The original results overlay is first laid over the preserved city screen, then any private
    /// victory or elimination card is laid over that. Its legacy destination was supplied as
    /// top, left, bottom, right = (25, 106, 435, 534), rather than as image coordinates.
    /// </summary>
    private void DrawEndgameBackground(SpriteBatch batch, Texture2D pixel)
    {
        DrawPanelArtwork(batch, pixel, _endgameBackground, EndgameLayout.Panel, 230);
    }

    private static void DrawEndgameNoticeName(
        SpriteBatch batch, PixelFont font, PlayerId playerId, string name)
    {
        var x = EndgameNoticeLayout.NameCenterX - name.Length * OriginalFontLayout.CellWidth / 2;
        font.Draw(batch, name, new Vector2(x, EndgameNoticeLayout.NameY), PlayerColors[playerId.Value], 1);
    }

    private void DrawEndgameAwards(
        SpriteBatch batch,
        PixelFont font,
        MatchOutcome outcome,
        PlayerId player,
        int row)
    {
        var awards = EndgamePresentation.AwardsForPlayer(outcome, player);
        for (var index = 0; index < awards.Count; index++)
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

    private void DrawEndgameStatistics(
        SpriteBatch batch,
        PixelFont font,
        MatchPlayerState player,
        int row)
    {
        if (_endgameSprites is not null)
            batch.Draw(_endgameSprites, EndgameLayout.StatisticsDestination(row),
                EndgameLayout.StatisticsSource, Color.White);

        long[] statistics =
        [
            player.Statistics.CashEarned,
            player.Statistics.CashSpent,
            player.Statistics.DamageInflicted,
            player.Statistics.Casualties,
            player.Statistics.Overthrows
        ];
        for (var index = 0; index < statistics.Length; index++)
        {
            var field = EndgameLayout.StatisticValueField(row, index);
            batch.Draw(_pixel, field, Color.Black);
            DrawPanelValue(font, batch, statistics[index].ToString(),
                field.Right, field.Y);
        }
    }

    private void AdvanceEndgamePresentation()
    {
        if (_showEndgameNotice)
        {
            var notice = _state is null ? null : EndgameNoticePresentation.For(_state);
            if (notice is not null && EndgameNoticePresentation.ContinuesToSummary(notice.Kind))
            {
                _showEndgameNotice = false;
                return;
            }
        }
        LeaveEndgame();
    }

    /// <summary>
    /// The last step out of the endgame, whichever presentation led to it.
    /// </summary>
    /// <remarks>
    /// An online match is over in its own right, but the session that drove it is still holding a
    /// token and a connection until somebody says so.
    /// </remarks>
    /// <summary>
    /// Returns to the title after a finished match.
    /// </summary>
    /// <remarks>
    /// The finished match is let go here, as <c>QuitToMainMenu</c> does. Keeping it meant Escape on
    /// the Setup or Online screen afterwards opened the in-game menu over the title, where SAVE GAME
    /// saved the finished match and QUIT warned that "your current game will be lost".
    /// </remarks>
    private void LeaveEndgame()
    {
        if (_session is not null)
        {
            EndOnlineMatch("THE MATCH IS OVER");
            return;
        }
        ResetTransientMatchUi();
        StopPlanningTimer();
        _state = null;
        _actions = null;
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
    public const int NameCenterX = 158;
    public const int NameY = 46;
    public static Rectangle Panel => new(110, 30, 312, 393);
    public static Rectangle Portrait => new(126, 54, 64, 64);
}

public sealed record EndgamePlayerRow(PlayerId Player, int Place, string Label);

public static class EndgamePresentation
{
    public const int VisibleAwardsPerPlayer = 3;

    public static IReadOnlyList<EndgamePlayerRow> Rows(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var outcome = state.Outcome ?? throw new ArgumentException("Match has not ended.", nameof(state));
        return outcome.Standings.Count > 0
            ? outcome.Standings.Select(standing => new EndgamePlayerRow(
                standing.Player, standing.Place,
                state.FindPlayer(standing.Player)!.Setup.Name))
            .ToArray()
            : state.Players.OrderByDescending(player => outcome.Winners.Contains(player.Id))
                .ThenBy(player => player.Id.Value)
                .Select(player => new EndgamePlayerRow(player.Id,
                    outcome.Winners.Contains(player.Id) ? 1 : 0,
                    player.Setup.Name))
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

    public static IReadOnlyList<EndgameAwardResult> AwardsForPlayer(
        MatchOutcome outcome,
        PlayerId player)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return outcome.Awards.Where(award => award.Recipients.Contains(player))
            .Take(VisibleAwardsPerPlayer).ToArray();
    }
}

public static class EndgameLayout
{
    public const int PlayerRows = MatchLimits.PlayerCount;
    public static Rectangle Panel => new(106, 25, 428, 410);
    // These are input regions, not the full painted button frames. The native pointer helper
    // receives the following top/left/bottom/right rectangles: Awards (33,428,81,476),
    // Stats (33,480,81,528), Done (377,428,425,528).
    public static Rectangle Awards => new(428, 33, 48, 48);
    public static Rectangle Stats => new(480, 33, 48, 48);
    public static Rectangle Done => new(428, 377, 100, 48);

    public static Rectangle Portrait(int row)
    {
        ValidateRow(row);
        return new Rectangle(132, 30 + row * 66, 64, 64);
    }

    public static Rectangle PlayerMarker(int row)
    {
        ValidateRow(row);
        return new Rectangle(113, 31 + row * 66, 16, 32);
    }

    public static Rectangle PlayerMarkerSource(PlayerId player, int place)
    {
        if (player.Value is < 0 or >= MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(player));
        if (place is < 1 or > MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(place));
        return new Rectangle((place - 1) * 16, 48 + player.Value * 32, 16, 32);
    }

    public static Vector2 Name(int row)
    {
        ValidateRow(row);
        return new Vector2(197, 38 + row * 66);
    }

    public static Rectangle Award(int row, int index)
    {
        ValidateRow(row);
        if (index is < 0 or >= EndgamePresentation.VisibleAwardsPerPlayer)
            throw new ArgumentOutOfRangeException(nameof(index));
        return new Rectangle(268 + index * 50, 38 + row * 66, 48, 48);
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

    public static Rectangle StatisticsSource => new(96, 112, 160, 64);

    public static Rectangle StatisticsDestination(int row)
    {
        ValidateRow(row);
        return new Rectangle(262, 30 + row * 66, 160, 64);
    }

    public static Rectangle StatisticValueField(int row, int statistic)
    {
        ValidateRow(row);
        if (statistic is < 0 or >= 5) throw new ArgumentOutOfRangeException(nameof(statistic));
        int[] left = [371, 371, 377, 383, 383];
        int[] yOffset = [37, 46, 58, 67, 79];
        int[] digits = [8, 8, 7, 6, 6];
        return new Rectangle(left[statistic], yOffset[statistic] + row * 66,
            digits[statistic] * OriginalFontLayout.CellWidth, 7);
    }

    private static void ValidateRow(int row)
    {
        if (row is < 0 or >= PlayerRows) throw new ArgumentOutOfRangeException(nameof(row));
    }
}

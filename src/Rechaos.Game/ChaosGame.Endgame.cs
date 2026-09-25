using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    // SCR-AWARDS-001, SCR-AWARDS-002: the Awards tab shows the victory splash again when one
    // player is left, and Done leaves at once from either view.
    private void HandleEndgameClick(Point point)
    {
        if (EndgameLayout.Awards.Contains(point))
        {
            PlayGeneralSound(AudioRouting.PointerPushSound());
            _showEndgameStats = false;
        }
        else if (EndgameLayout.Stats.Contains(point))
        {
            PlayGeneralSound(AudioRouting.PointerPushSound());
            _showEndgameStats = true;
        }
        else if (EndgameDone.Contains(point))
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
    private bool _showEndgameStats;

    /// <summary>
    /// The victory or elimination card: artwork, the overlord's portrait, and the name. The victory
    /// splash also paints its three bands in the survivor's colour (SCR-AWARDS-002).
    /// </summary>
    private void DrawEndgameNoticeCard(
        SpriteBatch batch, Texture2D pixel, PixelFont font, Texture2D? background,
        PlayerId player, int portraitId, string name, bool victory = false)
    {
        DrawPanelArtwork(batch, pixel, background, EndgameNoticeLayout.Panel, 255);
        if (victory)
            foreach (var band in EndgameNoticeLayout.VictoryColourBands)
                batch.Draw(pixel, band, PlayerColors[player.Value]);
        if (_uiSprites is not null)
            batch.Draw(_uiSprites, EndgameNoticeLayout.Portrait,
                OriginalSpriteLayout.OverlordPortrait(portraitId), Color.White);
        if (!victory)
            DrawBorder(batch, pixel, EndgameNoticeLayout.Portrait, PlayerColors[player.Value], 1);
        DrawEndgameNoticeName(batch, font, player, name);
    }

    private void DrawEndgame(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        if (_cityBackground is not null)
            batch.Draw(_cityBackground, new Rectangle(0, 0, 640, 460), Color.White);
        DrawEndgameBackground(batch, pixel);
        // RULE-AWARDS-002: with one player left, human or computer, the Awards tab shows that
        // player's victory splash in place of the table.
        if (!_showEndgameStats && EndgameNoticePresentation.Survivor(state) is { } survivor)
        {
            DrawEndgameNoticeCard(batch, pixel, font, _victoryBackground, survivor.Player,
                survivor.PortraitId, state.FindPlayer(survivor.Player)!.Setup.Name, victory: true);
            DrawSelectionLight(batch, pixel, OriginalSelectionLightLayout.EndgameTab(EndgameLayout.Awards));
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
        KeepRunRandomState();
        _state = null;
        _actions = null;
        _screens.Show(ClientScreen.Title);
    }
}

public sealed record EndgameNotice(PlayerId Player, short PortraitId);

public static class EndgameNoticePresentation
{
    /// <summary>
    /// RULE-AWARDS-002: the player whose victory splash the endgame shows, the only one still
    /// active when the match ended, whoever controls it (FND-AWARDS-004). None when several are
    /// active, as at the end of a timed scenario, or when none is.
    /// </summary>
    public static EndgameNotice? Survivor(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Outcome is null) return null;
        var active = state.Players.Where(player => player.Status == PlayerStatus.Active).ToArray();
        return active is [var survivor]
            ? new EndgameNotice(survivor.Id, survivor.Setup.PortraitId)
            : null;
    }
}

public static class EndgameNoticeLayout
{
    public const int NameCenterX = 158;
    public const int NameY = 46;
    public static Rectangle Panel => new(110, 30, 312, 393);
    public static Rectangle Portrait => new(126, 54, 64, 64);

    /// <summary>SCR-AWARDS-002: the splash's areas painted in the survivor's colour.</summary>
    public static IReadOnlyList<Rectangle> VictoryColourBands { get; } =
    [
        new(110, 30, 40, 12),
        new(110, 42, 13, 79),
        new(110, 121, 40, 302)
    ];
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

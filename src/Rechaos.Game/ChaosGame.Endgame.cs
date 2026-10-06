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
        PlayerId player, int portraitId, string name)
    {
        DrawPanelArtwork(batch, pixel, background, EndgameNoticeLayout.Panel, 255);
        // FND-AWARDS-004: the same slot colour as the table's colour fill, on the victory splash and,
        // in the same three areas, on the elimination card (SCR-OBJECTIVE-002, EXP-UI-032).
        foreach (var band in EndgameNoticeLayout.ColourBands)
            batch.Draw(pixel, band, SetupPlayerCardArtLayout.Colours[player.Value]);
        if (_uiSprites is not null)
            batch.Draw(_uiSprites, EndgameNoticeLayout.Portrait,
                OriginalSpriteLayout.OverlordPortrait(portraitId), Color.White);
        // SCR-OBJECTIVE-002, EXP-UI-018: the elimination card's name is in the plain font, and the
        // frame around the portrait is the splash's own. SCR-AWARDS-002: the victory splash draws
        // its name with the same plain font helper (FND-AWARDS-005, FND-RESEARCH-003).
        DrawEndgameNoticeName(batch, font, name);
    }

    // FND-AWARDS-004: resource 201 with its white keyed out, for the award icons.
    private Texture2D? _endgameKeyedSprites;

    private void DrawEndgame(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        DrawEndgameBackground(batch, pixel);
        // RULE-AWARDS-002: with one player left, human or computer, the Awards tab shows that
        // player's victory splash in place of the table.
        if (!_showEndgameStats && EndgameNoticePresentation.Survivor(state) is { } survivor)
        {
            DrawEndgameNoticeCard(batch, pixel, font, _victoryBackground, survivor.Player,
                survivor.PortraitId, state.FindPlayer(survivor.Player)!.Setup.Name);
            DrawEndgameTabMark(batch, stats: false);
            return;
        }

        var outcome = state.Outcome!;
        var rows = EndgamePresentation.Rows(state);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var player = state.FindPlayer(row.Player)!;
            // SCR-AWARDS-001, FND-AWARDS-004: the colour fill, the place marker of a ranked row, the
            // portrait, the name in the plain font, and the score caption and five-digit score of a
            // ranked row.
            batch.Draw(pixel, EndgameLayout.ColourFill(index), SetupPlayerCardArtLayout.Colours[row.Player.Value]);
            if (_endgameSprites is not null && row.Place > 0)
                batch.Draw(_endgameSprites, EndgameLayout.PlayerMarker(index),
                    EndgameLayout.PlayerMarkerSource(player.Id, row.Place), Color.White);
            if (_uiSprites is not null)
                batch.Draw(_uiSprites, EndgameLayout.Portrait(index),
                    OriginalSpriteLayout.OverlordPortrait(player.Setup.PortraitId), Color.White);
            font.Copy(batch, row.Label, EndgameLayout.Name(index).ToPoint(), OriginalFontLayout.PlainStrip);
            if (row.Place > 0)
            {
                font.Copy(batch, ExecutableStrings.ScoreCaption, EndgameLayout.ScoreCaption(index),
                    OriginalFontLayout.PlainStrip);
                font.Copy(batch, player.ScenarioScore.ToString().PadLeft(5), EndgameLayout.Score(index),
                    OriginalFontLayout.PlainStrip);
            }
            if (_showEndgameStats)
                DrawEndgameStatistics(batch, font, player, index);
            else
                DrawEndgameAwards(batch, font, outcome, row.Player, index);
        }
        DrawEndgameTabMark(batch, _showEndgameStats);
    }

    // FND-AWARDS-004: the 8-by-16 mark of the selected tab, copied from the interface sheet.
    private void DrawEndgameTabMark(SpriteBatch batch, bool stats)
    {
        if (_uiSprites is not null)
            batch.Draw(_uiSprites, EndgameLayout.TabMark(stats), OriginalSelectionLightLayout.CityLightSource,
                Color.White);
    }

    /// <summary>
    /// The results frame, loaded opaque into <c>(106, 25, 428, 410)</c> (FND-AWARDS-004), under any
    /// private victory or elimination card. The screen around it is black, as captured when the
    /// endgame follows the last turn's resolution (SCR-AWARDS-001, EXP-UI-017) or an elimination
    /// card (EXP-UI-034), and when the elimination card follows the resolution (SCR-OBJECTIVE-002,
    /// EXP-UI-018) or another human's planning, after the Ready card that blanks the screen
    /// (EXP-UI-032).
    /// </summary>
    private void DrawEndgameBackground(SpriteBatch batch, Texture2D pixel)
    {
        batch.Draw(pixel, new Rectangle(0, 0, VirtualInput.Width, VirtualInput.Height), Color.Black);
        DrawPanelArtwork(batch, pixel, _endgameBackground, EndgameLayout.Panel, 255);
    }

    private static void DrawEndgameNoticeName(SpriteBatch batch, PixelFont font, string name)
    {
        var x = EndgameNoticeLayout.NameCenterX - name.Length * OriginalFontLayout.CellWidth / 2;
        font.Copy(batch, name, new Point(x, EndgameNoticeLayout.NameY), OriginalFontLayout.PlainStrip);
    }

    private void DrawEndgameAwards(
        SpriteBatch batch,
        PixelFont font,
        MatchOutcome outcome,
        PlayerId player,
        int row)
    {
        // FND-AWARDS-004: the awards strip of resource 201 behind the row's icons.
        if (_endgameSprites is not null)
            batch.Draw(_endgameSprites, EndgameLayout.StripDestination(row), EndgameLayout.AwardsSource,
                Color.White);
        var awards = EndgamePresentation.AwardsForPlayer(outcome, player);
        for (var index = 0; index < awards.Count; index++)
        {
            var destination = EndgameLayout.Award(row, index);
            if (_endgameKeyedSprites is not null)
                batch.Draw(_endgameKeyedSprites, destination,
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
            batch.Draw(_endgameSprites, EndgameLayout.StripDestination(row),
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
        ClearPlanningTimer();
        ClearMatch();
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
    /// <summary>SCR-AWARDS-002: PX00202 and PX00203 are 311 x 393 (FMT-GFX-001, FND-GFX-005).</summary>
    public static Rectangle Panel => new(110, 30, 311, 393);
    public static Rectangle Portrait => new(126, 54, 64, 64);

    /// <summary>
    /// SCR-AWARDS-002, SCR-OBJECTIVE-002: the splash's areas painted in the colour of the survivor
    /// or of the eliminated player.
    /// </summary>
    public static IReadOnlyList<Rectangle> ColourBands { get; } =
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

    /// <summary>FND-AWARDS-004: the fill in the row player's colour.</summary>
    public static Rectangle ColourFill(int row)
    {
        ValidateRow(row);
        return new Rectangle(111, 31 + row * 66, 20, 62);
    }

    /// <summary>FND-AWARDS-004: the score caption of a ranked row.</summary>
    public static Point ScoreCaption(int row)
    {
        ValidateRow(row);
        return new Point(227, 62 + row * 66);
    }

    /// <summary>FND-AWARDS-004: the five-digit score of a ranked row.</summary>
    public static Point Score(int row)
    {
        ValidateRow(row);
        return new Point(227, 70 + row * 66);
    }

    /// <summary>FND-AWARDS-004: the mark of the selected tab, Awards or Stats.</summary>
    public static Rectangle TabMark(bool stats) => new(stats ? 520 : 468, 33, 8, 16);

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

    /// <summary>FND-AWARDS-004: the 48-by-48 icon at <c>(48 * code, 0)</c> of resource 201.</summary>
    public static Rectangle AwardSource(EndgameAward award) => award switch
    {
        EndgameAward.Fist => new Rectangle(0, 0, 48, 48),
        EndgameAward.Skull => new Rectangle(48, 0, 48, 48),
        EndgameAward.BigFatChicken => new Rectangle(96, 0, 48, 48),
        EndgameAward.DollarSign => new Rectangle(144, 0, 48, 48),
        EndgameAward.Safe => new Rectangle(192, 0, 48, 48),
        _ => throw new ArgumentOutOfRangeException(nameof(award))
    };

    public static Rectangle AwardsSource => new(96, 48, 160, 64);

    public static Rectangle StatisticsSource => new(96, 112, 160, 64);

    /// <summary>FND-AWARDS-004: where a row's awards or statistics strip is copied.</summary>
    public static Rectangle StripDestination(int row)
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

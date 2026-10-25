using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class PlayerRankingLayout
{
    private static readonly int[] PortraitLocalX = [98, 138, 178, 218, 258, 298];
    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Rectangle Ok => EquipmentCommandLayout.Ok;

    /// <summary>The length of a rail, in pixels, over which the scores are spread (SCR-OBJECTIVE-001).</summary>
    public const int RailLength = ScenarioScoreRail.Length;

    /// <summary>
    /// SCR-OBJECTIVE-001: the portrait of slot <paramref name="player"/>, <paramref name="offset"/>
    /// pixels down its rail.
    /// </summary>
    public static Rectangle Portrait(int player, int offset)
    {
        if (player is < 0 or >= MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(player));
        if (offset is < 0 or > RailLength)
            throw new ArgumentOutOfRangeException(nameof(offset));
        return SharedPanelLayout.At(PortraitLocalX[player], 18 + offset, 32, 32);
    }

    public static Rectangle Portrait(PlayerRankingEntry entry) =>
        Portrait(entry.Player.Value, entry.Offset);
}

/// <param name="Offset">
/// How far down its rail the portrait sits: 0 for the leader, lower in proportion to how far the
/// score trails the leader's (SCR-OBJECTIVE-001).
/// </param>
public sealed record PlayerRankingEntry(PlayerId Player, int Standing, long Score, int Offset = 0);

public static class PlayerRankingPresentation
{
    /// <summary>
    /// SCR-OBJECTIVE-001: the panel reads the scores the last evaluation stored (RULE-OBJECTIVE-002),
    /// so during a turn it shows them as they stood when the previous turn ended.
    /// </summary>
    public static IReadOnlyList<PlayerRankingEntry> Project(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var standings = EndgameRankingEvaluator.StoredStandings(state);
        var scored = state.Players
            .Where(player => player.Status == PlayerStatus.Active)
            .Select(player => (player.Id, Score: (long)player.ScenarioScore))
            .OrderBy(entry => entry.Id.Value)
            .ToArray();
        if (scored.Length == 0) return [];
        var high = scored.Max(entry => entry.Score);
        var low = scored.Min(entry => entry.Score);
        return scored.Select(entry => new PlayerRankingEntry(
                entry.Id,
                standings[entry.Id.Value],
                entry.Score,
                RailOffset(entry.Score, high, low)))
            .ToArray();
    }

    /// <summary>
    /// SCR-OBJECTIVE-001, FND-OBJECTIVE-005: the score's distance from the highest, scaled by a
    /// single-precision 140 / (high - low + 1) and cut toward zero; 70 when every score is equal.
    /// </summary>
    public static int RailOffset(long score, long high, long low) =>
        ScenarioScoreRail.Offset(score, high, low);
}

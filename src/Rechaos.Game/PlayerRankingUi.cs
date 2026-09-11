using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class PlayerRankingLayout
{
    private static readonly int[] PortraitX = [200, 241, 282, 322, 362, 402];
    public static Rectangle Panel => EquipmentCommandLayout.Panel;
    public static Rectangle Ok => EquipmentCommandLayout.Ok;

    public static Rectangle Portrait(int player, int standing)
    {
        if (player is < 0 or >= MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(player));
        if (standing is < 0 or >= MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(standing));
        return new Rectangle(PortraitX[player], 143 + standing * 28, 32, 32);
    }
}

public sealed record PlayerRankingEntry(PlayerId Player, int Standing, long Score);

public static class PlayerRankingPresentation
{
    public static IReadOnlyList<PlayerRankingEntry> Project(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var scored = state.Players
            .Where(player => player.Status == PlayerStatus.Active)
            .Select(player => (player.Id, Score: Score(state, player)))
            .OrderBy(entry => entry.Id.Value)
            .ToArray();
        return scored.Select(entry => new PlayerRankingEntry(
                entry.Id,
                scored.Count(candidate => candidate.Score > entry.Score),
                entry.Score))
            .ToArray();
    }

    private static long Score(MatchState state, MatchPlayerState player)
        => EndgameRankingEvaluator.Score(state, player);
}

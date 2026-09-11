namespace Rechaos.Core.GameModel;

public enum EndgameAward : byte
{
    Skull,
    Fist,
    DollarSign,
    Safe,
    BigFatChicken
}

public sealed record EndgameAwardResult(
    EndgameAward Award,
    long Value,
    IReadOnlyList<PlayerId> Recipients);

/// <summary>Native endgame superlatives in their recovered award-table priority.</summary>
public static class EndgameAwardEvaluator
{
    public const long FistThreshold = 5;
    public const long SkullThreshold = 50;
    public const long ChickenThreshold = 10;
    public const long DollarThreshold = 0;
    public const long SafeCeiling = 999_999;

    public static IReadOnlyList<EndgameAwardResult> Evaluate(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var players = state.Players.OrderBy(player => player.Id.Value).ToArray();
        if (players.Length == 0) return [];

        var results = new List<EndgameAwardResult>(5);
        AddMaximum(EndgameAward.Fist, player => player.Statistics.Overthrows, FistThreshold);
        AddMaximum(EndgameAward.Skull, player => player.Statistics.DamageInflicted, SkullThreshold);
        AddMaximum(EndgameAward.BigFatChicken,
            player => player.Statistics.TimesHidden, ChickenThreshold);
        AddMaximum(EndgameAward.DollarSign, player => player.Statistics.CashSpent, DollarThreshold);
        AddMinimum(EndgameAward.Safe, player => player.Statistics.CashSpent, SafeCeiling);
        return results;

        void AddMaximum(
            EndgameAward award,
            Func<MatchPlayerState, long> selector,
            long threshold)
        {
            var value = players.Max(selector);
            if (value < threshold) return;
            results.Add(new EndgameAwardResult(
                award, value, players.Where(player => selector(player) == value).Select(player => player.Id).ToArray()));
        }

        void AddMinimum(
            EndgameAward award,
            Func<MatchPlayerState, long> selector,
            long ceiling)
        {
            var value = players.Min(selector);
            if (value > ceiling) return;
            results.Add(new EndgameAwardResult(
                award, value, players.Where(player => selector(player) == value).Select(player => player.Id).ToArray()));
        }
    }
}

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

/// <summary>Manual-defined endgame superlatives with provisional tie handling.</summary>
public static class EndgameAwardEvaluator
{
    public static IReadOnlyList<EndgameAwardResult> Evaluate(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var players = state.Players.OrderBy(player => player.Id.Value).ToArray();
        if (players.Length == 0) return [];

        var results = new List<EndgameAwardResult>(5);
        AddMaximum(EndgameAward.Skull, player => player.Statistics.DamageInflicted, requireActivity: true);
        AddMaximum(EndgameAward.Fist, player => player.Statistics.Overthrows);
        AddMaximum(EndgameAward.DollarSign, player => player.Statistics.CashSpent);
        AddMinimum(EndgameAward.Safe, player => player.Statistics.CashSpent);
        AddMaximum(EndgameAward.BigFatChicken, player => player.Statistics.TimesHidden, requireActivity: true);
        return results;

        void AddMaximum(
            EndgameAward award,
            Func<MatchPlayerState, long> selector,
            bool requireActivity = false)
        {
            var value = players.Max(selector);
            if (requireActivity && value == 0) return;
            results.Add(new EndgameAwardResult(
                award, value, players.Where(player => selector(player) == value).Select(player => player.Id).ToArray()));
        }

        void AddMinimum(EndgameAward award, Func<MatchPlayerState, long> selector)
        {
            var value = players.Min(selector);
            results.Add(new EndgameAwardResult(
                award, value, players.Where(player => selector(player) == value).Select(player => player.Id).ToArray()));
        }
    }
}

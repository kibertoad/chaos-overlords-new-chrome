namespace Rechaos.Core.GameModel;

public static partial class AiTurnPlanner
{
    /// <summary>
    /// Proof that RULE-AI-003 cached a player's sector weights, which the family handlers read in
    /// place of a fresh <c>visible_weight</c>. Only <see cref="Cache"/> makes one, so the handlers
    /// cannot run on a row nobody filled.
    /// </summary>
    internal sealed class CachedSectorWeights
    {
        private CachedSectorWeights(PlayerId player) => Player = player;

        /// <summary>The player whose row was cached.</summary>
        internal PlayerId Player { get; }

        /// <summary>RULE-AI-003: caches the player's 64 sector weights.</summary>
        internal static CachedSectorWeights Cache(MatchState state, PlayerId observer)
        {
            ArgumentNullException.ThrowIfNull(state);
            ComputeVisibleWeights(state, observer, state.AiPlanning.SectorWeightRow(observer));
            return new CachedSectorWeights(observer);
        }
    }

    /// <summary>
    /// RULE-AI-004's <c>visible_weight</c> for every sector: the first gang of another player the
    /// observer can see, in player and roster order, gives 10 when its owner is a human the
    /// observer is hostile to and 1 otherwise; a sector with no visible gang gives 0.
    /// </summary>
    /// <remarks>
    /// One sweep over the rosters in that order fills every sector: a sector's first visible gang
    /// is the first one the sweep meets, and a filled sector is never 0, so it is not written again.
    /// </remarks>
    internal static void ComputeVisibleWeights(
        MatchState state,
        PlayerId observer,
        Span<byte> weights)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (weights.Length != MatchLimits.SectorCount)
            throw new ArgumentException("A player has one weight per sector.", nameof(weights));
        weights.Clear();
        for (var playerIndex = 0; playerIndex < MatchLimits.PlayerCount; playerIndex++)
        {
            var ownerId = new PlayerId(playerIndex);
            if (ownerId == observer || state.FindPlayer(ownerId) is not { } owner) continue;
            var weight = checked((byte)VisibleOpponentWeight(state, observer, ownerId));
            foreach (var gang in owner.Gangs)
                if (gang.IsActive
                    && weights[gang.SectorId] == 0
                    && state.CanPlayerDetectGang(observer, gang.Id))
                    weights[gang.SectorId] = weight;
        }
    }
}

using Rechaos.Core.GameModel;

namespace Rechaos.Tests;

/// <summary>
/// Runs the family handlers of a planning pass on a record the test set up by hand, after caching
/// the sector weights the pass's RULE-AI-003 step would have cached.
/// </summary>
internal static class AiHandlerPass
{
    public static void Run(MatchState match, PlayerId player)
    {
        match.AiPlanning.CacheSectorWeights(player, AiTurnPlanner.VisibleWeights(match, player));
        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);
    }
}

using Rechaos.Core.GameModel;

namespace Rechaos.Tests;

/// <summary>
/// Runs the family handlers of a planning pass on a record the test set up by hand, on the sector
/// weights the pass's RULE-AI-003 step caches.
/// </summary>
internal static class AiHandlerPass
{
    public static void Run(MatchState match, PlayerId player) =>
        AiTurnPlanner.PrepareRecoveredFamilyCommands(
            match, AiTurnPlanner.CachedSectorWeights.Cache(match, player));
}

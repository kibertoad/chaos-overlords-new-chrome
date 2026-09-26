using Rechaos.Core.GameModel;

namespace Rechaos.Tests;

internal static class AiPlanningFixtures
{
    /// <summary>
    /// Gives a record the family an earlier pass settled (RULE-AI-002): the family is written and
    /// the slot's needs_family flag cleared, so the next dispatch keeps the family. Setting the
    /// family alone leaves a flag the first pass raised on slot 0, and the dispatch would replace it.
    /// </summary>
    public static void SeedFamily(this AiPlanningState planning, PlayerId player, int gangSlot, int family)
    {
        planning.SetFamily(player, gangSlot, family);
        planning.ClearNeedsFamily(player, gangSlot);
    }
}

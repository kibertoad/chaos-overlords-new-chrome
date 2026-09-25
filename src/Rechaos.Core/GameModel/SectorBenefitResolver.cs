namespace Rechaos.Core.GameModel;

/// <summary>
/// Activates completed sites at the original pre-planning sector rebuild.
/// Completion during Instant leaves the site pending for the rest of that turn.
/// </summary>
internal static class SectorBenefitResolver
{
    public static void ActivatePending(MatchState state)
    {
        var completedSites = CompletedLastTurn(state);
        foreach (var sector in state.Sectors.OrderBy(value => value.Id))
        {
            if (sector.Owner is not { } owner) continue;
            var player = state.FindPlayer(owner)!;
            foreach (var site in sector.Sites.OrderBy(value => value.Slot))
            {
                var siteId = sector.Id * MatchLimits.SitesPerSector + site.Slot;
                if (!completedSites.Contains(siteId)
                    || site.Resistance != 0
                    || site.InfluencedBy is not null) continue;
                var definition = state.Definitions.Site(site.DefinitionId);
                site.InfluencedBy = owner;
                player.Support = checked(player.Support + definition.Support);
            }
        }
    }

    /// <summary>The sites whose Resistance reached zero during the turn that has just ended.</summary>
    /// <remarks>
    /// Walked back from the end rather than filtered, the way every other reader of the log does
    /// it. The history is append-only in turn order and this runs at every Upkeep, so scanning all
    /// of it made one turn's cost grow with the number of turns already played — over a full-length
    /// match that is the whole log re-read a couple of hundred times.
    /// </remarks>
    private static HashSet<int> CompletedLastTurn(MatchState state)
    {
        var completedTurn = state.Coordinator.Turn - 1;
        var completed = new HashSet<int>();
        var events = state.Events;
        for (var index = events.Count - 1; index >= 0; index--)
        {
            var value = events[index];
            if (value.Turn < completedTurn) break;
            if (value.Turn == completedTurn
                && value.Kind == GameEventKind.CommandResolved
                && value.Action == GangAction.Influence
                && value.Target.Kind == CommandTargetKind.Site
                && value.Resolution is { PreviousValue: > 0, ResultValue: 0 })
                completed.Add(value.Target.Id);
        }
        return completed;
    }
}

namespace Rechaos.Core.GameModel;

/// <summary>
/// Activates completed sites at the original pre-planning sector rebuild.
/// Completion during Instant leaves the site pending for the rest of that turn.
/// </summary>
internal static class SectorBenefitResolver
{
    public static void ActivatePending(MatchState state)
    {
        var completedSites = state.Events
            .Where(value => value.Turn == state.Coordinator.Turn - 1
                && value.Kind == GameEventKind.CommandResolved
                && value.Action == GangAction.Influence
                && value.Target.Kind == CommandTargetKind.Site
                && value.Resolution is { PreviousValue: > 0, ResultValue: 0 })
            .Select(value => value.Target.Id)
            .ToHashSet();
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
                var definition = state.Definitions.Sites.Single(
                    value => value.Id == site.DefinitionId);
                site.InfluencedBy = owner;
                player.Support = checked(player.Support + definition.Support);
                sector.Tolerance = checked(sector.Tolerance + definition.Tolerance);
            }
        }
    }
}

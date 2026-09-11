using Rechaos.Core.Assets;

namespace Rechaos.Core.GameModel;

public sealed partial class MatchState
{
    private static void ValidateDefinitionsAndCapacities(
        OriginalData definitions,
        IReadOnlyList<MatchPlayerState> players,
        IReadOnlyList<MatchSectorState> sectors)
    {
        foreach (var player in players)
        {
            if (player.Gangs.Count > MatchLimits.GangsPerPlayer)
                throw new ArgumentException($"Player {player.Id} exceeds gang capacity.", nameof(players));
            if (player.HirePool.Count > MatchLimits.HireOffersPerPlayer)
                throw new ArgumentException($"Player {player.Id} exceeds hire-pool capacity.", nameof(players));
            if (player.HireOfferSlots.Any(slot =>
                    slot.GangDefinitionId.HasValue && slot.ExcludedDefinitionId.HasValue))
                throw new ArgumentException($"Player {player.Id} has an invalid hire-offer slot.", nameof(players));
            if (player.HireOfferSlots.Any(slot =>
                    (slot.GangDefinitionId is { } offered
                        && (offered == 0 || !definitions.Gangs.Any(definition => definition.Id == offered)))
                    || (slot.ExcludedDefinitionId is { } excluded
                        && (excluded == 0 || !definitions.Gangs.Any(definition => definition.Id == excluded)))
                    || (slot.LegacyReplacementDefinitionId is { } replacement
                        && (replacement == 0 || !definitions.Gangs.Any(definition => definition.Id == replacement)))))
                throw new ArgumentException($"Player {player.Id} has an unknown hire-offer slot value.", nameof(players));
            if (player.HirePool.Count != player.HirePool.Distinct().Count())
                throw new ArgumentException($"Player {player.Id} has duplicate hire offers.", nameof(players));
            var reservedHireOffers = player.HireOfferSlots
                .SelectMany(slot => new[] { slot.GangDefinitionId, slot.LegacyReplacementDefinitionId })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToArray();
            if (reservedHireOffers.Length != reservedHireOffers.Distinct().Count())
                throw new ArgumentException($"Player {player.Id} has duplicate visible or reserved hire offers.", nameof(players));
            if (player.SnubbedHireOffer is { } snubbed
                && (snubbed == 0
                    || !definitions.Gangs.Any(definition => definition.Id == snubbed)
                    || player.SnubbedHireOfferSlot is { } snubSlot
                        && (snubSlot is < 0 or >= MatchLimits.HireOffersPerPlayer
                            || player.HireOfferSlots[snubSlot].GangDefinitionId != snubbed)))
                throw new ArgumentException($"Player {player.Id} has invalid snubbed hire-offer state.", nameof(players));
            if (player.PendingHires.Count > 1)
                throw new ArgumentException($"Player {player.Id} has more than one pending hire.", nameof(players));
            if (player.PendingHires.Any(hire => hire.OfferSlot >= 0
                    && (hire.OfferSlot >= MatchLimits.HireOffersPerPlayer
                        || player.HireOfferSlots[hire.OfferSlot].GangDefinitionId != hire.GangDefinitionId)))
                throw new ArgumentException($"Player {player.Id} has an invalid pending-hire slot.", nameof(players));
            if (player.PendingHires.Any(hire =>
                    !definitions.Gangs.Any(definition => definition.Id == hire.GangDefinitionId)))
                throw new ArgumentException($"Player {player.Id} has an invalid pending hire.", nameof(players));
            if (player.PendingHires.Any(hire => hire.TargetSectorId is < 0 or >= MatchLimits.SectorCount))
                throw new ArgumentException($"Player {player.Id} has an invalid pending-hire sector.", nameof(players));
            if (player.Gangs.Any(gang => gang.Owner != player.Id))
                throw new ArgumentException($"Player {player.Id} contains a gang owned by another player.", nameof(players));
            if (player.Gangs.Any(gang => !definitions.Gangs.Any(definition => definition.Id == gang.DefinitionId)))
                throw new ArgumentException($"Player {player.Id} contains an unknown gang definition.", nameof(players));
            if (player.Gangs.Any(gang => EquippedItemIds(gang).Any(itemId =>
                    itemId < 0 || itemId >= definitions.Items.Count || definitions.Items[itemId].Type == 99)))
                throw new ArgumentException($"Player {player.Id} contains invalid equipped item state.", nameof(players));
            if (player.ResearchProgress.Any(pair =>
                    !IsActualItem(definitions, pair.Key) || pair.Value <= 0))
                throw new ArgumentException($"Player {player.Id} contains invalid research progress.", nameof(players));
            if (player.ResearchedItems.Any(itemId => !IsActualItem(definitions, itemId)))
                throw new ArgumentException($"Player {player.Id} contains an invalid researched item.", nameof(players));
            if (player.ResearchProgress.Keys.Any(player.ResearchedItems.Contains))
                throw new ArgumentException($"Player {player.Id} has overlapping active and completed research.", nameof(players));
            if (player.Inventory.Any(pair => !IsActualItem(definitions, pair.Key) || pair.Value <= 0))
                throw new ArgumentException($"Player {player.Id} contains invalid inventory state.", nameof(players));
        }

        var overcrowded = players.SelectMany(player => player.Gangs)
            .GroupBy(gang => (gang.Owner, gang.SectorId))
            .FirstOrDefault(group => group.Count() > MatchLimits.FriendlyGangsPerSector);
        if (overcrowded is not null)
            throw new ArgumentException($"Player {overcrowded.Key.Owner} exceeds sector {overcrowded.Key.SectorId} capacity.", nameof(players));

        if (sectors.SelectMany(sector => sector.Sites)
            .Any(site => !definitions.Sites.Any(definition => definition.Id == site.DefinitionId)))
            throw new ArgumentException("A sector contains an unknown site definition.", nameof(sectors));
        var playerIds = players.Select(player => player.Id).ToHashSet();
        if (sectors.Any(sector => sector.Owner is { } owner && !playerIds.Contains(owner)))
            throw new ArgumentException("A sector owner is not part of the match.", nameof(sectors));
        if (sectors.SelectMany(sector => sector.Sites)
            .Any(site => site.InfluencedBy is { } owner && !playerIds.Contains(owner)))
            throw new ArgumentException("A site influencer is not part of the match.", nameof(sectors));
        if (sectors.Any(sector => sector.Sites.Any(site =>
                site.InfluencedBy is { } influencer && sector.Owner != influencer)))
            throw new ArgumentException(
                "An influenced site must belong to the player controlling its sector.",
                nameof(sectors));
    }

    private static IEnumerable<short> EquippedItemIds(MatchGangState gang)
    {
        if (gang.WeaponItemId is { } weapon) yield return weapon;
        if (gang.ArmorItemId is { } armor) yield return armor;
        if (gang.MiscellaneousItemId is { } miscellaneous) yield return miscellaneous;
    }

    private static bool IsActualItem(OriginalData definitions, short itemId) =>
        itemId >= 0 && itemId < definitions.Items.Count && definitions.Items[itemId].Type != 99;
}

namespace Rechaos.Core.GameModel;

public sealed partial class MatchState
{
    // Both lookups sit on the hot path of every validation and resolution step. They are plain
    // loops rather than LINQ so that finding a gang allocates nothing, while still refusing a
    // duplicate identifier the way SingleOrDefault did.
    public MatchPlayerState? FindPlayer(PlayerId id)
    {
        MatchPlayerState? found = null;
        foreach (var player in Players)
        {
            if (player.Id != id) continue;
            if (found is not null) throw MoreThanOne();
            found = player;
        }
        return found;
    }

    public MatchGangState? FindGang(GangId id)
    {
        MatchGangState? found = null;
        foreach (var player in Players)
        {
            foreach (var gang in player.Gangs)
            {
                if (gang.Id != id) continue;
                if (found is not null) throw MoreThanOne();
                found = gang;
            }
        }
        return found;
    }

    public MatchSiteState? FindSite(int id) => id is >= 0 and < MatchLimits.SiteCount
        ? Sectors[id / MatchLimits.SitesPerSector].Sites[id % MatchLimits.SitesPerSector]
        : null;

    private static InvalidOperationException MoreThanOne() =>
        new("Sequence contains more than one matching element");
}

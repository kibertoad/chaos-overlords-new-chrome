namespace Rechaos.Core.GameModel;

public sealed partial class MatchState
{
    public MatchPlayerState? FindPlayer(PlayerId id) =>
        Players.SingleOrDefault(player => player.Id == id);

    public MatchGangState? FindGang(GangId id) =>
        Players.SelectMany(player => player.Gangs).SingleOrDefault(gang => gang.Id == id);

    public MatchSiteState? FindSite(int id) => id is >= 0 and < MatchLimits.SiteCount
        ? Sectors[id / MatchLimits.SitesPerSector].Sites[id % MatchLimits.SitesPerSector]
        : null;
}

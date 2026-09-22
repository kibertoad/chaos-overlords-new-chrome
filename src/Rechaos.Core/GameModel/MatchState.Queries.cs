namespace Rechaos.Core.GameModel;

public sealed partial class MatchState
{
    // These dictionaries are established by the constructor and maintained at the two roster
    // mutation points, so validation and resolution do not rescan the entire match roster.
    public MatchPlayerState? FindPlayer(PlayerId id) => _playersById.GetValueOrDefault(id);

    public MatchGangState? FindGang(GangId id) => _gangsById.GetValueOrDefault(id);

    public MatchSiteState? FindSite(int id) => id is >= 0 and < MatchLimits.SiteCount
        ? Sectors[id / MatchLimits.SitesPerSector].Sites[id % MatchLimits.SitesPerSector]
        : null;
}

namespace Rechaos.Core.GameModel;

public sealed partial class MatchState
{
    // Both lookups sit on the hot path of every validation and resolution step, so they are
    // constant-time reads of state the constructor established rather than scans of the roster.
    // MatchGangIndex carries the gang lookup across roster additions and slot replacements; the
    // player roster is fixed for the life of the match, and SetController preserves player ids.
    public MatchPlayerState? FindPlayer(PlayerId id) => _playersById.GetValueOrDefault(id);

    public MatchGangState? FindGang(GangId id) => _gangIndex.Find(id);

    public MatchSiteState? FindSite(int id) => id is >= 0 and < MatchLimits.SiteCount
        ? Sectors[id / MatchLimits.SitesPerSector].Sites[id % MatchLimits.SitesPerSector]
        : null;
}

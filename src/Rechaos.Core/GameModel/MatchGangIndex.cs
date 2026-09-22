namespace Rechaos.Core.GameModel;

/// <summary>
/// The match-wide gang lookup behind <see cref="MatchState.FindGang"/>, and the owner of the
/// invariant that lets it be a lookup at all: one gang per <see cref="GangId"/> across every
/// roster in the match.
/// </summary>
/// <remarks>
/// A <see cref="MatchState"/> builds exactly one index over its rosters and joins every player to
/// it, so a player belongs to a single match and the index cannot drift from the rosters it
/// describes. Both roster mutation points report here before they touch a roster, which keeps a
/// refused gang from leaving the roster and the index disagreeing.
/// </remarks>
internal sealed class MatchGangIndex
{
    private const string DuplicateIdentifier = "Gang identifiers must be unique across the match.";

    private readonly Dictionary<GangId, MatchGangState> _gangsById = [];

    private MatchGangIndex()
    {
    }

    /// <summary>
    /// Indexes every gang already on the rosters of <paramref name="players"/>, which is also how
    /// a match validates that the rosters it was handed carry distinct gang identifiers.
    /// </summary>
    internal static MatchGangIndex ForRosters(IReadOnlyList<MatchPlayerState> players)
    {
        var index = new MatchGangIndex();
        foreach (var player in players)
        {
            foreach (var gang in player.Gangs)
            {
                if (!index._gangsById.TryAdd(gang.Id, gang))
                    throw new ArgumentException(DuplicateIdentifier, nameof(players));
            }
        }
        return index;
    }

    internal MatchGangState? Find(GangId id) => _gangsById.GetValueOrDefault(id);

    /// <summary>Indexes a gang joining a roster, refusing an identifier the match already uses.</summary>
    internal void Add(MatchGangState gang)
    {
        if (!_gangsById.TryAdd(gang.Id, gang))
            throw new ArgumentException(DuplicateIdentifier, nameof(gang));
    }

    /// <summary>
    /// Repoints the index at the <paramref name="replacement"/> taking over
    /// <paramref name="replaced"/>'s roster slot. A hire reusing the retired gang's identifier is
    /// the ordinary case and stays a single overwrite.
    /// </summary>
    internal void Replace(MatchGangState replaced, MatchGangState replacement)
    {
        if (!_gangsById.TryGetValue(replaced.Id, out var indexed) || !ReferenceEquals(indexed, replaced))
            throw new InvalidOperationException("The replaced gang is not indexed by this match.");
        if (replaced.Id == replacement.Id)
        {
            _gangsById[replacement.Id] = replacement;
            return;
        }
        if (_gangsById.ContainsKey(replacement.Id))
            throw new ArgumentException(DuplicateIdentifier, nameof(replacement));
        _gangsById.Remove(replaced.Id);
        _gangsById.Add(replacement.Id, replacement);
    }
}

using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// What dragging a gang across the detailed sector view paints with: the commands the gang may
/// still be given, the sectors those commands reach, and the gangs drawn on the cards behind the
/// pointer.
/// </summary>
/// <remarks>
/// <para>
/// Taking this is expensive — <see cref="CommandOptionCatalog.LegalCommands"/> expands every
/// command shape the gang has and puts each candidate through the validator — and it used to run
/// once per frame for as long as the button was held. It is taken once when the drag starts
/// instead.
/// </para>
/// <para>
/// <see cref="Describes"/> is what makes holding on to it safe. A drag outlives the frame it
/// started on, and the board under it does not stand still: a resolved online turn hands the
/// interface a different state, the planning clock ending a hot-seat turn advances the state
/// already held, and the arrow keys scroll the 3-by-3 minimap under the pointer. Painting last
/// turn's destinations over this turn's map — while the drop itself is carried out against the
/// live state — costs more than the projection does, so the drawing code asks whether the
/// projection still describes what it is drawing and takes a fresh one when it does not.
/// </para>
/// </remarks>
public sealed class SectorGangDragProjection
{
    private readonly View _view;

    private SectorGangDragProjection(
        View view,
        IReadOnlyList<GameCommand> legalCommands,
        IReadOnlySet<int> legalSectors,
        IReadOnlyList<MatchGangState> visibleGangs)
    {
        _view = view;
        LegalCommands = legalCommands;
        LegalSectors = legalSectors;
        VisibleGangs = visibleGangs;
    }

    /// <summary>Every command the dragged gang may still be given, for the drop feedback to read.</summary>
    public IReadOnlyList<GameCommand> LegalCommands { get; }

    /// <summary>The sectors of those commands, as the minimap highlights them.</summary>
    public IReadOnlySet<int> LegalSectors { get; }

    /// <summary>The gangs the selected sector shows the dragged gang's owner, in card order.</summary>
    public IReadOnlyList<MatchGangState> VisibleGangs { get; }

    /// <summary>Projects what a drag of <paramref name="gang"/> over sector <paramref name="cursor"/> paints with.</summary>
    public static SectorGangDragProjection For(MatchState state, MatchGangState gang, int cursor)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gang);
        var legalCommands = CommandOptionCatalog.LegalCommands(state, gang.Owner, gang.Id);
        return new SectorGangDragProjection(
            View.Of(state, gang.Id, cursor),
            legalCommands,
            SectorMapGangDrop.Destinations(legalCommands, gang.SectorId),
            SectorGangView.Visible(state, gang.Owner, cursor));
    }

    /// <summary>
    /// Answers whether this projection is still of the gang, state and selected sector being drawn.
    /// </summary>
    public bool Describes(MatchState state, GangId gang, int cursor)
    {
        ArgumentNullException.ThrowIfNull(state);
        return _view == View.Of(state, gang, cursor);
    }

    /// <summary>
    /// Everything a projection was taken from, as far as it can change while the button is held.
    /// </summary>
    /// <remarks>
    /// The state is held by identity because a resolved online turn hands the interface a
    /// different object, and its turn position by value because a hot-seat turn advances the
    /// object already held. Either way the commands the gang has, and the gangs it is drawn
    /// among, are no longer the ones that were projected.
    /// </remarks>
    private readonly record struct View(
        MatchState State,
        GangId Gang,
        int Cursor,
        int Turn,
        TurnPhase Phase,
        PlayerId? ActivePlayer)
    {
        public static View Of(MatchState state, GangId gang, int cursor) => new(
            state, gang, cursor,
            state.Coordinator.Turn, state.Coordinator.Phase, state.Coordinator.ActivePlayer);
    }
}

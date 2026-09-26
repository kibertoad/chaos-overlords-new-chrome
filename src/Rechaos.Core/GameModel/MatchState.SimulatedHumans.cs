namespace Rechaos.Core.GameModel;

public sealed partial class MatchState
{
    private readonly HashSet<PlayerId> _simulatedHumans = [];

    /// <summary>
    /// Lets the computer planner play a human seat, for simulations that need an opponent the
    /// computer players treat as human.
    /// </summary>
    /// <remarks>
    /// The seat stays <see cref="PlayerController.Human"/>, so every rule and AI query that asks
    /// whether a player is human, such as the hunters' choice of targets and the end of the match
    /// when the only human is eliminated, answers as it would for a person. Only the checks that
    /// keep the computer planner off human seats let it through. The mark lives in this object
    /// alone: a save, a clone or a replay journal does not carry it, so a replay of such a match
    /// refuses the first planning step of the seat.
    /// </remarks>
    internal void SimulateHuman(PlayerId playerId)
    {
        var player = FindPlayer(playerId) ?? throw new ArgumentOutOfRangeException(nameof(playerId));
        if (player.Setup.Controller != PlayerController.Human)
            throw new ArgumentException("Only a human seat can be simulated.", nameof(playerId));
        _simulatedHumans.Add(playerId);
    }

    /// <summary>Whether the computer planner plays this seat: a computer seat or a simulated human.</summary>
    internal bool IsPlannedByComputer(PlayerId playerId) =>
        FindPlayer(playerId)?.Setup.Controller == PlayerController.Computer
        || _simulatedHumans.Contains(playerId);
}

namespace Rechaos.Game;

/// <summary>How much of the hire dock in front of the player is theirs to use.</summary>
public enum HireDockAccess
{
    /// <summary>There is no turn behind the dock, so there is nothing to show.</summary>
    Closed,

    /// <summary>The offers may be opened and read, but none may be taken or refused.</summary>
    ReadOnly,

    /// <summary>The offers are the player's to hire or snub.</summary>
    Open
}

/// <summary>What the hire dock allows, given whose turn the match in front of the player is.</summary>
public static class HireDockPolicy
{
    /// <summary>
    /// Answers what the dock allows.
    /// </summary>
    /// <remarks>
    /// A submitted online turn keeps the dock readable. Its offers were drawn when the turn reached
    /// Command, on every client at once, and the state they were drawn against is the one the sealed
    /// turn will resolve from — so a player waiting on the other seats can still look up what they
    /// are about to pay for, and looking cannot put this client on a different state from the rest.
    /// Hiring or snubbing is what has to wait, because the document the server already holds is the
    /// turn: a change made after it went out would show in the interface, never be sent, and leave
    /// the player believing they ordered something they did not.
    /// </remarks>
    /// <param name="hasMatch">Whether there is a match state to read at all.</param>
    /// <param name="holdsTurn">Whether the turn is still the player's to change.</param>
    /// <param name="turnIsSubmitted">Whether an online turn is submitted and waiting to seal.</param>
    public static HireDockAccess Access(bool hasMatch, bool holdsTurn, bool turnIsSubmitted)
    {
        if (!hasMatch) return HireDockAccess.Closed;
        if (holdsTurn) return HireDockAccess.Open;
        return turnIsSubmitted ? HireDockAccess.ReadOnly : HireDockAccess.Closed;
    }
}

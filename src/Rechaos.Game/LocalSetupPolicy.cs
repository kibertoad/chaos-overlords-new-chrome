using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class LocalSetupPolicy
{
    public const int DefaultHumanPlayerCount = 1;
    public const int MaximumPlayerNameCharacters = 10;

    public static string DefaultPlayerName(int playerIndex)
    {
        if (playerIndex is < 0 or >= MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(playerIndex));
        return $"PLAYER#{playerIndex + 1}";
    }

    /// <summary>
    /// Applies the original setup dialog's accepted text to a player name.
    /// </summary>
    /// <remarks>
    /// Its input buffer starts empty and the native copy helper does nothing when it remains so.
    /// A space is not empty: it is a glyph the native helper stores in the fixed record.
    /// </remarks>
    public static string NameAfterModalEntry(string existingName, string enteredName)
    {
        ArgumentNullException.ThrowIfNull(existingName);
        ArgumentNullException.ThrowIfNull(enteredName);
        return enteredName.Length == 0 ? existingName : enteredName;
    }

    /// <summary>
    /// RULE-SETUP-009: steps a human's portrait by <paramref name="delta"/>, wrapping at both ends,
    /// until it reaches one no human slot holds, the slot's own included. An empty slot holds none.
    /// </summary>
    public static short StepPortrait(
        IReadOnlyList<short> portraits, IReadOnlyList<int> humanSlots, int slot, int delta)
    {
        ArgumentNullException.ThrowIfNull(portraits);
        ArgumentNullException.ThrowIfNull(humanSlots);
        var held = humanSlots.Select(human => (int)portraits[human]).ToHashSet();
        var face = (int)portraits[slot];
        do
        {
            face += delta;
            if (face < 0) face = PlayerPortraitLayout.SelectableCount - 1;
            if (face >= PlayerPortraitLayout.SelectableCount) face = 0;
        }
        while (held.Contains(face));
        return checked((short)face);
    }

    /// <summary>RULE-SETUP-010: the lowest portrait no human slot holds, which Add gives the new human.</summary>
    public static short LowestFreePortrait(IReadOnlyList<short> portraits, IReadOnlyList<int> humanSlots)
    {
        ArgumentNullException.ThrowIfNull(portraits);
        ArgumentNullException.ThrowIfNull(humanSlots);
        var held = humanSlots.Select(human => (int)portraits[human]).ToHashSet();
        var face = 0;
        while (held.Contains(face)) face++;
        return checked((short)face);
    }
}

/// <summary>Hover text for the local setup's roster controls (RULE-SETUP-009, RULE-SETUP-010).</summary>
public static class SetupRosterTooltip
{
    public static IReadOnlyList<string> PortraitArrow { get; } =
    [
        "PORTRAIT",
        "STEPS TO THE NEXT PORTRAIT NO OTHER",
        "PLAYER HOLDS, WRAPPING AT EITHER END."
    ];

    public static IReadOnlyList<string> AddPlayer { get; } =
    [
        "ADD PLAYER",
        "PUTS A HUMAN IN THE LOWEST EMPTY COLOUR",
        "WITH THE LOWEST PORTRAIT NOBODY HOLDS.",
        "BEGIN KEEPS THE PLAYERS FOR THE NEXT GAME."
    ];

    /// <summary>RULE-SETUP-002: added under a local setup's scenario description.</summary>
    public const string ScenarioRemembered = "THE NEXT NEW GAME STARTS ON YOUR CHOICE.";
}

/// <summary>
/// RULE-SETUP-010: the roster Begin saves (humans, portraits and names), which the next local setup
/// of the session opens with.
/// </summary>
public sealed record LocalSetupSnapshot(
    IReadOnlyList<int> HumanSlots, IReadOnlyList<short> Portraits, IReadOnlyList<string> Names)
{
    /// <summary>The first setup of a session: one human in slot 0 with portrait 0, default names.</summary>
    public static LocalSetupSnapshot Initial { get; } = new(
        [0],
        Enumerable.Range(0, MatchLimits.PlayerCount).Select(index => checked((short)index)).ToArray(),
        Enumerable.Range(0, MatchLimits.PlayerCount).Select(LocalSetupPolicy.DefaultPlayerName).ToArray());
}

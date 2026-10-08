using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class LocalSetupPolicy
{
    public const int DefaultHumanPlayerCount = 1;
    public const int MaximumPlayerNameCharacters = 10;

    // FND-SETUP-017, EXP-UI-006: string 61, which ends in a space and a number sign, then the
    // slot's number from 1.
    public static string DefaultPlayerName(int playerIndex)
    {
        if (playerIndex is < 0 or >= MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(playerIndex));
        return $"PLAYER #{playerIndex + 1}";
    }

    /// <summary>
    /// FND-UI-022, FND-UI-068: applies the text the name dialog's edit control holds when OK
    /// is pressed to a player name.
    /// </summary>
    /// <remarks>
    /// The copy helper's buffer starts empty and it does nothing when the text is still empty. A
    /// space is not empty: it is a glyph the helper stores in the fixed record. Otherwise the
    /// helper keeps the first ten characters of the text, however long it is, and stores a space
    /// for every character outside space to <c>Z</c>, which includes every byte from 0x80 because
    /// the helper reads them as signed.
    /// </remarks>
    public static string NameAfterModalEntry(string existingName, string enteredName)
    {
        ArgumentNullException.ThrowIfNull(existingName);
        ArgumentNullException.ThrowIfNull(enteredName);
        if (enteredName.Length == 0) return existingName;
        var length = Math.Min(enteredName.Length, MaximumPlayerNameCharacters);
        return string.Create(length, enteredName, static (cells, text) =>
        {
            for (var index = 0; index < cells.Length; index++)
                cells[index] = text[index] is >= ' ' and <= 'Z' ? text[index] : ' ';
        });
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

/// <summary>Hover text for the local setup.</summary>
public static class SetupRosterTooltip
{
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

using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// DEV-UI-005: a sector's owner in words. The city map (SCR-UI-003) shows an owner by the colour
/// of the sector's interior and the sector view (SCR-UI-004) by the colour of its owner strip, so
/// a player who cannot tell the colours apart reads the owner from a tooltip or the message line.
/// </summary>
public static class SectorOwnerText
{
    /// <summary>The tooltip shown while the pointer rests on a sector.</summary>
    public static IReadOnlyList<string> Tooltip(MatchState state, PlayerId viewer, int sectorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var code = ChaosGame.SectorCode(sectorId);
        if (state.Sectors[sectorId].Owner is not { } owner)
            return [$"SECTOR {code}", "NO OWNER"];
        return
        [
            $"SECTOR {code}",
            $"OWNER: {Name(state, owner)}{(owner == viewer ? " (YOU)" : "")}",
            $"OVERLORD BAR SEAT {owner.Value + 1}"
        ];
    }

    /// <summary>
    /// The message line after a key moves the selected sector, clipped to
    /// <see cref="CityStatusMessage.MaxCharacters"/>.
    /// </summary>
    public static string MessageLine(MatchState state, PlayerId viewer, int sectorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var code = ChaosGame.SectorCode(sectorId);
        var line = state.Sectors[sectorId].Owner switch
        {
            null => $"{code} HAS NO OWNER",
            { } owner when owner == viewer => $"{code} OWNED BY YOU",
            { } owner => $"{code} OWNED BY {Name(state, owner)}"
        };
        return CityStatusMessage.Clip(line);
    }

    private static string Name(MatchState state, PlayerId owner) =>
        state.FindPlayer(owner)?.Setup.Name.ToUpperInvariant() ?? $"OVERLORD {owner.Value + 1}";
}

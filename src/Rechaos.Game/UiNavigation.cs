using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public enum ClientScreen
{
    Title,
    Setup,
    City,
    Commands,
    Hire,
    Events,
    Sector,
    Gang,
    Finance,
    Ranking,
    Items,
    Give,
    CombatSummary,
    Search,
    Handoff,
    Endgame
}

public sealed class ScreenRouter
{
    public ClientScreen Current { get; private set; } = ClientScreen.Title;

    public void Show(ClientScreen screen) => Current = screen;

    public bool Back()
    {
        if (Current == ClientScreen.Title) return false;
        Current = Current is ClientScreen.Events or ClientScreen.Commands or ClientScreen.Hire
            or ClientScreen.Sector or ClientScreen.Gang or ClientScreen.Finance or ClientScreen.Ranking
            or ClientScreen.Items or ClientScreen.Give
            or ClientScreen.CombatSummary
            or ClientScreen.Search
            ? Current == ClientScreen.Give ? ClientScreen.Items : ClientScreen.City
            : ClientScreen.Title;
        return true;
    }
}

public static class VirtualInput
{
    public const int Width = 640;
    public const int Height = 460;

    public static Matrix Transform(Viewport viewport)
    {
        var scale = MathF.Min(viewport.Width / (float)Width, viewport.Height / (float)Height);
        return Matrix.CreateScale(scale) * Matrix.CreateTranslation(
            (viewport.Width - Width * scale) / 2,
            (viewport.Height - Height * scale) / 2,
            0);
    }

    public static bool TryMap(Viewport viewport, Point physical, out Point virtualPoint)
    {
        var scale = MathF.Min(viewport.Width / (float)Width, viewport.Height / (float)Height);
        var left = (viewport.Width - Width * scale) / 2;
        var top = (viewport.Height - Height * scale) / 2;
        var x = (physical.X - left) / scale;
        var y = (physical.Y - top) / scale;
        virtualPoint = new Point((int)x, (int)y);
        return x >= 0 && x < Width && y >= 0 && y < Height;
    }
}

/// <summary>Native layout of the 8x8 sector cells in the PX10000-PX10006 city layers.</summary>
public static class CityMapLayout
{
    public const int Left = 2;
    public const int Top = 44;
    public const int TileWidth = 54;
    public const int TileHeight = 52;

    public static Rectangle Source(int sectorId)
    {
        ValidateSector(sectorId);
        return new Rectangle(
            sectorId % 8 * TileWidth,
            sectorId / 8 * TileHeight,
            TileWidth,
            TileHeight);
    }

    public static Rectangle Destination(int sectorId)
    {
        var source = Source(sectorId);
        return new Rectangle(Left + source.X, Top + source.Y, source.Width, source.Height);
    }

    public static int OwnershipSheet(PlayerId? owner) => owner?.Value + 1 ?? 0;

    public static bool TrySectorAt(Point point, out int sectorId)
    {
        var x = point.X - Left;
        var y = point.Y - Top;
        if (x < 0 || x >= TileWidth * 8 || y < 0 || y >= TileHeight * 8)
        {
            sectorId = -1;
            return false;
        }
        sectorId = y / TileHeight * 8 + x / TileWidth;
        return true;
    }

    private static void ValidateSector(int sectorId)
    {
        if (sectorId is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));
    }
}

public static class OriginalSpriteLayout
{
    public static Rectangle PolicePatrolCar => new(116, 0, 48, 64);

    public static Rectangle SitePortrait(int definitionId)
    {
        if (definitionId is < 0 or >= 22) throw new ArgumentOutOfRangeException(nameof(definitionId));
        return new Rectangle(0, definitionId * 64, 120, 64);
    }

    public static Rectangle GangPortrait(int definitionId)
    {
        if (definitionId is < 0 or >= 90) throw new ArgumentOutOfRangeException(nameof(definitionId));
        return new Rectangle(definitionId % 10 * 64, definitionId / 10 * 64, 64, 64);
    }
}

public static class SectorGangView
{
    public const int MaximumPortraits = 10;
    public const int MaximumSearchRows = 7;

    public static IReadOnlyList<MatchGangState> Visible(
        MatchState state,
        PlayerId viewer,
        int sectorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.FindPlayer(viewer) is null) throw new ArgumentOutOfRangeException(nameof(viewer));
        if (sectorId is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));
        return state.Players.SelectMany(player => player.Gangs)
            .Where(gang => gang.IsActive && gang.SectorId == sectorId
                && (gang.Owner == viewer || state.CanPlayerDetectGang(viewer, gang.Id)))
            .OrderBy(gang => gang.Owner.Value)
            .ThenBy(gang => gang.Id.Value)
            .ToArray();
    }

    public static Rectangle Portrait(int index)
    {
        if (index is < 0 or >= MaximumPortraits) throw new ArgumentOutOfRangeException(nameof(index));
        return new Rectangle(18 + index * 40, 370, 36, 36);
    }

    public static Rectangle SearchPortrait(int index)
    {
        if (index is < 0 or >= MaximumSearchRows) throw new ArgumentOutOfRangeException(nameof(index));
        return new Rectangle(18, 107 + index * 40, 36, 36);
    }
}

public static class GangArtLayout
{
    public static Rectangle SelectedEquipmentPortrait => new(558, 58, 56, 56);
}

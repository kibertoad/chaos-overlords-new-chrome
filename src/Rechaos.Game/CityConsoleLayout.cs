using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public enum CityConsoleControl
{
    Events,
    Comlink,
    Combat,
    Finance,
    GangHire,
    RankingSearch,
    Done,
    GameInfo
}

public enum CityConsoleAction
{
    Events,
    ComlinkView,
    ComlinkSend,
    CombatSummary,
    CombatDetail,
    FinanceCity,
    FinanceSector,
    Gangs,
    Hire,
    Ranking,
    Search,
    Done,
    GameInfo
}

public static partial class CityConsoleLayout
{
    public static Rectangle Events => new(500, 126, 48, 48);
    public static Rectangle Comlink => new(552, 126, 48, 48);
    public static Rectangle Combat => new(500, 178, 48, 48);
    public static Rectangle Finance => new(552, 178, 48, 48);
    public static Rectangle GangHire => new(500, 230, 48, 48);
    public static Rectangle RankingSearch => new(552, 230, 48, 48);
    public static Rectangle Done => new(500, 282, 100, 48);
    public static Rectangle GameInfo => new(588, 41, 26, 34);

    public static Rectangle ComlinkView => new(552, 126, 48, 33);
    public static Rectangle ComlinkSend => new(552, 159, 48, 15);
    public static Rectangle CombatSummary => new(500, 178, 48, 33);
    public static Rectangle CombatDetail => new(500, 211, 48, 15);
    public static Rectangle FinanceCity => new(552, 178, 48, 33);
    public static Rectangle FinanceSector => new(552, 211, 48, 15);
    public static Rectangle Gangs => new(500, 230, 48, 33);
    public static Rectangle Hire => new(500, 263, 48, 15);
    public static Rectangle Ranking => new(552, 230, 48, 25);
    public static Rectangle Search => new(552, 255, 48, 23);

    public static CityConsoleControl? HitTest(Point point)
    {
        foreach (var control in Enum.GetValues<CityConsoleControl>())
            if (Destination(control).Contains(point)) return control;
        return null;
    }

    public static CityConsoleAction ActionAt(Point point)
    {
        var control = HitTest(point)
            ?? throw new ArgumentOutOfRangeException(nameof(point));
        return control switch
        {
            CityConsoleControl.Events => CityConsoleAction.Events,
            CityConsoleControl.Comlink => point.Y > Comlink.Y + 32
                ? CityConsoleAction.ComlinkSend
                : CityConsoleAction.ComlinkView,
            CityConsoleControl.Combat => point.Y > Combat.Y + 32
                ? CityConsoleAction.CombatDetail
                : CityConsoleAction.CombatSummary,
            CityConsoleControl.Finance => point.Y > Finance.Y + 32
                ? CityConsoleAction.FinanceSector
                : CityConsoleAction.FinanceCity,
            CityConsoleControl.GangHire => point.Y > GangHire.Y + 32
                ? CityConsoleAction.Hire
                : CityConsoleAction.Gangs,
            CityConsoleControl.RankingSearch => point.Y > RankingSearch.Y + 24
                ? CityConsoleAction.Search
                : CityConsoleAction.Ranking,
            CityConsoleControl.Done => CityConsoleAction.Done,
            CityConsoleControl.GameInfo => CityConsoleAction.GameInfo,
            _ => throw new ArgumentOutOfRangeException(nameof(point))
        };
    }

    public static Rectangle Destination(CityConsoleControl control) => control switch
    {
        CityConsoleControl.Events => Events,
        CityConsoleControl.Comlink => Comlink,
        CityConsoleControl.Combat => Combat,
        CityConsoleControl.Finance => Finance,
        CityConsoleControl.GangHire => GangHire,
        CityConsoleControl.RankingSearch => RankingSearch,
        CityConsoleControl.Done => Done,
        CityConsoleControl.GameInfo => GameInfo,
        _ => throw new ArgumentOutOfRangeException(nameof(control))
    };

    public static Rectangle PressedSource(CityConsoleControl control) => control switch
    {
        CityConsoleControl.Events => new Rectangle(0, 512, 48, 48),
        CityConsoleControl.Comlink => new Rectangle(48, 512, 48, 48),
        CityConsoleControl.Combat => new Rectangle(96, 512, 48, 48),
        CityConsoleControl.Finance => new Rectangle(144, 512, 48, 48),
        CityConsoleControl.GangHire => new Rectangle(192, 512, 48, 48),
        CityConsoleControl.RankingSearch => new Rectangle(240, 512, 48, 48),
        CityConsoleControl.Done => new Rectangle(288, 512, 100, 48),
        CityConsoleControl.GameInfo => new Rectangle(190, 386, 26, 34),
        _ => throw new ArgumentOutOfRangeException(nameof(control))
    };
}

using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static partial class OriginalSpriteLayout
{
    private const int GangCardSourceLeft = 162;
    private const int GangCardSourceTop = 15;
    private const int GangCardWidth = 74;
    private const int GangCardHeight = 110;
    private const int GangActionSourceTop = 125;
    private const int GangActionWidth = 64;
    private const int GangActionHeight = 9;

    public static Rectangle GangCardFrame =>
        new(GangCardSourceLeft, GangCardSourceTop, GangCardWidth, GangCardHeight);

    public static Rectangle GangActionStrip(GangAction action) =>
        new(GangCardSourceLeft, GangActionSourceTop + (int)action * GangActionHeight,
            GangActionWidth, GangActionHeight);
}

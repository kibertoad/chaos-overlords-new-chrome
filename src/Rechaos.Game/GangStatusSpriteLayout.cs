using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public static partial class OriginalSpriteLayout
{
    public static Rectangle AssignedGangStatus => GangStatus(0);
    public static Rectangle ContestedAssignedGangStatus => GangStatus(1);
    public static Rectangle IdleGangStatus => GangStatus(2);
    public static Rectangle ContestedIdleGangStatus => GangStatus(3);
    public static Rectangle IncomingGangStatus => GangStatus(8);

    public static Rectangle GangStatus(int state)
    {
        if (state is < 0 or > 8) throw new ArgumentOutOfRangeException(nameof(state));
        return new Rectangle(492, 67 + state * 20, 20, 20);
    }
}

using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public static class SetupPlayerCardArtLayout
{
    public static Rectangle ArrowOverlaySource => new(220, 138, 64, 62);

    public static Rectangle PortraitDestination(int player)
    {
        var face = PlayerPortraitLayout.SetupLarge(player);
        return new Rectangle(face.X, face.Y + 3, 64, 60);
    }

    public static Rectangle PortraitSource(int portraitId)
    {
        if (portraitId is < 0 or >= PlayerPortraitLayout.Count)
            throw new ArgumentOutOfRangeException(nameof(portraitId));
        return new Rectangle(portraitId * 32, 480, 32, 30);
    }
}

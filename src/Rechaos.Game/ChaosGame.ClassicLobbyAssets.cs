using Microsoft.Xna.Framework.Graphics;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    // Drawn only in the optional presentation mode; it is not a dependency of online transport.
    private Texture2D? ClassicLobbyBackground => Texture(OriginalBitmap.ClassicLobbyBackground);
}

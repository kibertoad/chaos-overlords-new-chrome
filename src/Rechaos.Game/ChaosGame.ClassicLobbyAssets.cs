using Microsoft.Xna.Framework.Graphics;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    // Loaded only for the optional presentation mode; it is not a dependency of online transport.
    private Texture2D? ClassicLobbyBackground => Texture("PX00144.bmp");
}

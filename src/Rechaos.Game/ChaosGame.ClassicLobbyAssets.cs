using Microsoft.Xna.Framework.Graphics;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    // Loaded only for the optional presentation mode; it is not a dependency of online transport.
    private Texture2D? _classicLobbyBackground;
    private Texture2D? ClassicLobbyBackground => _classicLobbyBackground ??= LoadTexture("PX00144.bmp");
}

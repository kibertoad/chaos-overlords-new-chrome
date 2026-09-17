using Microsoft.Xna.Framework;

namespace Rechaos.Game;

/// <summary>
/// Native destination and local coordinate system shared by the PX050xx panels.
/// Descendant geometry must be expressed relative to this origin so the panel
/// artwork, dynamic clearing, drawing, and hit testing move atomically.
/// </summary>
public static class SharedPanelLayout
{
    public const int Left = 104;
    public const int Top = 124;
    public const int Width = 344;
    public const int Height = 209;
    public static Rectangle Panel => new(Left, Top, Width, Height);
    public static Rectangle StandardPortrait => At(26, 17, 64, 64);
    public static Rectangle CommandCancel => At(32, 137, 49, 24);
    public static Rectangle CommandOk => At(32, 169, 49, 24);
    public static int X(int localX) => Left + localX;
    public static int Y(int localY) => Top + localY;
    public static Rectangle At(int localX, int localY, int width, int height) =>
        new(X(localX), Y(localY), width, height);
}

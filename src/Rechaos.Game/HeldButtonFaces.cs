using Microsoft.Xna.Framework;

namespace Rechaos.Game;

/// <summary>The face kinds the original's held-button helper <c>fn_00418821</c> takes (FND-UI-062).</summary>
public enum HeldButtonKind
{
    /// <summary>OK, the command panels' confirm, Send, and the panels' exit and close faces.</summary>
    Confirm = 0,

    /// <summary>Cancel, and the exit of Detailed Combat.</summary>
    Cancel = 1,

    /// <summary>The sector view's back control.</summary>
    SectorBack = 3,

    /// <summary>ALL in Site Search.</summary>
    SearchAll = 4,

    /// <summary>NONE in Site Search.</summary>
    SearchNone = 5,
}

/// <summary>
/// The two faces of <c>PX00129</c> the held-button helper copies for each kind (FND-UI-062): the
/// lit face while the pointer is over the held control, and the plain face while it is off it and
/// after the button comes up, wherever it is released.
/// </summary>
public static class HeldButtonFaces
{
    public static Rectangle Lit(HeldButtonKind kind) => kind switch
    {
        HeldButtonKind.Confirm => new Rectangle(0, 386, 50, 23),
        HeldButtonKind.Cancel => new Rectangle(0, 409, 50, 23),
        HeldButtonKind.SectorBack => new Rectangle(120, 205, 32, 63),
        HeldButtonKind.SearchAll => new Rectangle(97, 560, 50, 23),
        HeldButtonKind.SearchNone => new Rectangle(197, 560, 50, 23),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static Rectangle Plain(HeldButtonKind kind) => kind switch
    {
        HeldButtonKind.Confirm => new Rectangle(50, 386, 50, 23),
        HeldButtonKind.Cancel => new Rectangle(50, 409, 50, 23),
        HeldButtonKind.SectorBack => new Rectangle(460, 211, 32, 63),
        HeldButtonKind.SearchAll => new Rectangle(147, 560, 50, 23),
        HeldButtonKind.SearchNone => new Rectangle(247, 560, 50, 23),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>
    /// What the helper copies to the window for a held face (FND-UI-062): the lit face while the
    /// pointer is over it and the plain face otherwise, each at the face's top-left corner at the
    /// size of its image on the sheet. A panel's face tests a rectangle one pixel narrower and
    /// shorter than the image (FND-UI-067).
    /// </summary>
    public static (Rectangle Destination, Rectangle Source) Drawn(HeldButtonKind kind, Rectangle face, bool pointerInside)
    {
        var source = pointerInside ? Lit(kind) : Plain(kind);
        return (new Rectangle(face.X, face.Y, source.Width, source.Height), source);
    }
}

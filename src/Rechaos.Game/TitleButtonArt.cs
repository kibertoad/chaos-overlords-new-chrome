using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rechaos.Game;

/// <summary>
/// The title menu's buttons (DEV-UI-019), drawn in the setup screen's material: the face of its
/// Begin button stretched by tiling to each button's size, and a label in the yellow lettering the
/// setup and city console buttons carry, cut letter by letter out of their art.
/// </summary>
/// <remarks>
/// No font of the game has these letters; each label is part of its button's picture. The labels
/// of the setup controls (PX00140) and of the console's command buttons (PX00129) between them
/// hold every letter the menu needs but Q, which is the O with a tail added.
/// </remarks>
internal static class TitleButtonArt
{
    /// <summary>The rows a letter takes: seven of ink and the shadow under them.</summary>
    public const int GlyphHeight = 8;

    /// <summary>The columns a space adds to the gap every letter leaves after it.</summary>
    public const int SpaceWidth = 3;

    /// <summary>Where a label starts inside its button, as Begin's does.</summary>
    public static readonly Point LabelOffset = new(4, 3);

    /// <summary>
    /// A piece of the setup screen's background drawn at any size: its corners as they are, and its
    /// edges and inside repeated between them.
    /// </summary>
    /// <param name="Source">The piece in the background art.</param>
    /// <param name="Edge">How far its bevelled edge reaches in.</param>
    /// <param name="TopEdge">The stretch of its top edge that is repeated.</param>
    /// <param name="Interior">The stone repeated over its inside.</param>
    public readonly record struct Frame(Rectangle Source, int Edge, Rectangle TopEdge, Rectangle Interior);

    private static readonly Rectangle BeginFace = SetupButtonLayout.Start;

    /// <summary>
    /// The face of the setup screen's Begin button. Its top edge is taken right of the label, so
    /// the top of Begin's letters is not repeated, and its inside from the blank stone under it.
    /// </summary>
    public static readonly Frame Face = new(BeginFace, 3,
        new Rectangle(BeginFace.X + 30, BeginFace.Y, BeginFace.Width - 33, 3),
        new Rectangle(BeginFace.X + 3, BeginFace.Y + 14, BeginFace.Width - 6, BeginFace.Height - 17));

    private static readonly Rectangle BeginHousing = new(358, 366, 216, 63);

    /// <summary>
    /// The stone housing that holds Begin and Cancel. What shows of its inside between the title's
    /// buttons is filled from the marble band along the top of the setup screen's left panel, wide
    /// enough to run under New Game without repeating.
    /// </summary>
    public static readonly Frame Housing = new(BeginHousing, 8,
        new Rectangle(BeginHousing.X + 8, BeginHousing.Y, BeginHousing.Width - 16, 8),
        new Rectangle(76, 24, 232, 10));

    /// <summary>How far a housing reaches past the buttons it holds, as Begin's housing does.</summary>
    public static readonly Point HousingMargin = new(11, 8);

    public enum Sheet
    {
        /// <summary>PX00129, whose command buttons spell EVENTS.</summary>
        Console,

        /// <summary>PX00140, the setup screen's pressed controls.</summary>
        SetupControls
    }

    /// <summary>A letter's cell: its ink, the shadow under and right of it, and the gap after it.</summary>
    public readonly record struct Glyph(Sheet Sheet, Rectangle Source);

    private static Glyph Console(int x) => new(Sheet.Console, new Rectangle(x, 516, 5, GlyphHeight));

    private static Glyph Setup(int x, int y, int width) =>
        new(Sheet.SetupControls, new Rectangle(x, y, width, GlyphHeight));

    private static readonly Dictionary<char, Glyph> Glyphs = new()
    {
        // EVENTS
        ['E'] = Console(4),
        ['N'] = Console(19),
        ['T'] = Console(24),
        ['S'] = Console(29),
        // KILL 'EM ALL
        ['L'] = Setup(12, 4, 5),
        ['A'] = Setup(38, 4, 6),
        // REMOVE
        ['R'] = Setup(224, 27, 5),
        ['M'] = Setup(234, 27, 6),
        ['O'] = Setup(240, 27, 6),
        // BEGIN
        ['G'] = Setup(234, 51, 6),
        ['I'] = Setup(240, 51, 2),
        // POWER
        ['P'] = Setup(115, 100, 5),
        ['W'] = Setup(125, 100, 7),
        // DOMINANCE
        ['D'] = Setup(116, 132, 5),
        // 2 MINUTES
        ['U'] = Setup(134, 212, 5),
        // HOMICIDAL MANIAC
        ['H'] = Setup(4, 236, 6),
    };

    /// <summary>The tail drawn on the O to make a Q, as (column, row, is shadow).</summary>
    private static readonly (int X, int Y, bool Shadow)[] QTail =
        [(3, 6, false), (4, 7, false), (4, 6, true), (5, 7, true), (4, 8, true), (5, 8, true)];

    /// <summary>The yellow of the letters' brightest stems, and their shadow's black.</summary>
    private static readonly Color QTailInk = new(246, 197, 57);
    private static readonly Color QTailShadow = new(8, 8, 8);

    /// <summary>The letters the lettering has, Q included.</summary>
    public static IReadOnlyCollection<char> Letters => [.. Glyphs.Keys, 'Q'];

    /// <summary>The source of <paramref name="letter"/>, Q's being the O's.</summary>
    public static Glyph GlyphOf(char letter) => Glyphs[letter == 'Q' ? 'O' : letter];

    /// <summary>
    /// Whether a pixel of a label belongs to the lettering: its yellow, the half-tones its edges
    /// take with the grey, and its black shadow. The stone around it is left behind.
    /// </summary>
    public static bool IsLettering(Color pixel) =>
        pixel.R - pixel.B > 30 || Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) < 50;

    public static bool CanSpell(string text) => text.All(c => c == ' ' || Letters.Contains(c));

    public static int Width(string text) =>
        text.Sum(c => c == ' ' ? SpaceWidth : GlyphOf(c).Source.Width);

    /// <summary>
    /// Cuts the letters out of their sheets into one strip, transparent where the stone was, and
    /// returns it with each letter's cell in it, one row taller than a glyph for Q's tail.
    /// </summary>
    public static (Texture2D Strip, Dictionary<char, Rectangle> Cells) Cut(
        GraphicsDevice device, Texture2D console, Texture2D setupControls)
    {
        var letters = Letters.Order().ToArray();
        var width = letters.Sum(letter => GlyphOf(letter).Source.Width);
        // One row more than a glyph: Q's tail reaches under the shadow.
        var height = GlyphHeight + 1;
        var strip = new Color[width * height];
        var sheets = new Dictionary<Sheet, (Color[] Pixels, int Width)>
        {
            [Sheet.Console] = (Pixels(console), console.Width),
            [Sheet.SetupControls] = (Pixels(setupControls), setupControls.Width),
        };
        var cells = new Dictionary<char, Rectangle>();
        var left = 0;
        foreach (var letter in letters)
        {
            var glyph = GlyphOf(letter);
            var (pixels, sheetWidth) = sheets[glyph.Sheet];
            for (var y = 0; y < glyph.Source.Height; y++)
            for (var x = 0; x < glyph.Source.Width; x++)
            {
                var pixel = pixels[(glyph.Source.Y + y) * sheetWidth + glyph.Source.X + x];
                if (IsLettering(pixel)) strip[y * width + left + x] = pixel;
            }
            if (letter == 'Q')
            {
                foreach (var (x, y, shadow) in QTail)
                    strip[y * width + left + x] = shadow ? QTailShadow : QTailInk;
            }
            cells[letter] = new Rectangle(left, 0, glyph.Source.Width, height);
            left += glyph.Source.Width;
        }
        var texture = new Texture2D(device, width, height);
        texture.SetData(strip);
        return (texture, cells);
    }

    private static Color[] Pixels(Texture2D texture)
    {
        var pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);
        return pixels;
    }
}

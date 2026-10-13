using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void DrawTitle(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (TitleBackground is not null)
        {
            batch.Draw(TitleBackground, new Rectangle(0, 0, 640, 460), Color.White);
            // The copyright line moves left with the menu, cut from the art and drawn again.
            batch.Draw(pixel, TitleCopyrightArt, Color.Black);
            batch.Draw(TitleBackground, TitleCopyright, TitleCopyrightArt, Color.White);
        }
        else
        {
            batch.Draw(pixel, new Rectangle(92, 72, 456, 112), new Color(0, 0, 0, 210));
            DrawCentered(font, batch, "CHAOS OVERLORDS", 103, Color.Gold, 3);
        }
        if (SetupBackground is { } stone)
        {
            // The housings stand behind the buttons as Begin's does behind it, the lower one over
            // the foot of the upper.
            DrawStoneFrame(batch, stone, TitleButtonArt.Housing, TitleUpperHousing);
            DrawStoneFrame(batch, stone, TitleButtonArt.Housing, TitleLowerHousing);
        }
        foreach (var (button, label) in TitleButtons)
            DrawTitleButton(batch, pixel, font, button, label);
        DrawTitleDinoLogo(batch);
        DrawTitleMessage(batch, pixel, font);
        DrawTitleVersion(batch, font);
    }

    internal static (Rectangle Button, string Label)[] TitleButtons =>
    [
        (TitleNewGame, "NEW GAME"), (TitleLoadGame, "LOAD"), (TitleOnline, "ONLINE"),
        (TitleOptions, "OPTIONS"), (TitleHelp, "HELP"), (TitleIntro, "INTRO"), (TitleQuit, "QUIT")
    ];

    /// <summary>The line between the two housings, halfway between the rows they hold.</summary>
    private static int TitleHousingSeam => (TitleOnline.Bottom + TitleOptions.Y) / 2;

    /// <summary>The housing of New Game, Load and Online.</summary>
    internal static Rectangle TitleUpperHousing => Rectangle.Union(TitleNewGame, TitleOnline) is var rows
        ? new Rectangle(rows.X - TitleButtonArt.HousingMargin.X, rows.Y - TitleButtonArt.HousingMargin.Y,
            rows.Width + 2 * TitleButtonArt.HousingMargin.X,
            TitleHousingSeam - rows.Y + TitleButtonArt.HousingMargin.Y)
        : default;

    /// <summary>The housing of the bottom row.</summary>
    internal static Rectangle TitleLowerHousing => Rectangle.Union(TitleOptions, TitleQuit) is var row
        ? new Rectangle(row.X - TitleButtonArt.HousingMargin.X, TitleHousingSeam,
            row.Width + 2 * TitleButtonArt.HousingMargin.X,
            row.Bottom + TitleButtonArt.HousingMargin.Y - TitleHousingSeam)
        : default;

    private Texture2D? _titleLettering;
    private Dictionary<char, Rectangle> _titleLetterCells = [];

    /// <summary>
    /// DEV-UI-019: a title button in the setup screen's stone and yellow lettering, or the drawn
    /// button when the art is missing.
    /// </summary>
    private void DrawTitleButton(SpriteBatch batch, Texture2D pixel, PixelFont font, Rectangle button, string label)
    {
        if (SetupBackground is not { } stone || UiSprites is not { } console || SetupControls is not { } controls)
        {
            DrawButton(batch, pixel, font, button, label, true);
            return;
        }
        if (_titleLettering is null)
            (_titleLettering, _titleLetterCells) = TitleButtonArt.Cut(GraphicsDevice, console, controls);
        DrawStoneFrame(batch, stone, TitleButtonArt.Face, button);
        var x = button.X + TitleButtonArt.LabelOffset.X;
        var y = button.Y + TitleButtonArt.LabelOffset.Y;
        foreach (var letter in label)
        {
            if (letter == ' ')
            {
                x += TitleButtonArt.SpaceWidth;
                continue;
            }
            var cell = _titleLetterCells[letter];
            batch.Draw(_titleLettering, new Rectangle(x, y, cell.Width, cell.Height), cell, Color.White);
            x += cell.Width;
        }
    }

    /// <summary>
    /// <paramref name="art"/> at the size of <paramref name="area"/>: its corners as they are, its
    /// edges and its inside repeated to fill the length between them.
    /// </summary>
    private static void DrawStoneFrame(SpriteBatch batch, Texture2D stone, TitleButtonArt.Frame art, Rectangle area)
    {
        var edge = art.Edge;
        var source = art.Source;
        var inside = new Rectangle(area.X + edge, area.Y + edge, area.Width - 2 * edge, area.Height - 2 * edge);
        Tile(batch, stone, art.Interior, inside);
        Tile(batch, stone, art.TopEdge, new Rectangle(inside.X, area.Y, inside.Width, edge));
        Tile(batch, stone, new Rectangle(source.X + edge, source.Bottom - edge, source.Width - 2 * edge, edge),
            new Rectangle(inside.X, inside.Bottom, inside.Width, edge));
        var sideRows = new Rectangle(source.X, source.Y + edge, edge, source.Height - 2 * edge);
        Tile(batch, stone, sideRows, new Rectangle(area.X, inside.Y, edge, inside.Height));
        Tile(batch, stone, sideRows with { X = source.Right - edge }, new Rectangle(inside.Right, inside.Y, edge, inside.Height));
        var corner = new Point(edge, edge);
        batch.Draw(stone, new Rectangle(area.Location, corner), new Rectangle(source.Location, corner), Color.White);
        batch.Draw(stone, new Rectangle(new Point(inside.Right, area.Y), corner),
            new Rectangle(new Point(source.Right - edge, source.Y), corner), Color.White);
        batch.Draw(stone, new Rectangle(new Point(area.X, inside.Bottom), corner),
            new Rectangle(new Point(source.X, source.Bottom - edge), corner), Color.White);
        batch.Draw(stone, new Rectangle(new Point(inside.Right, inside.Bottom), corner),
            new Rectangle(new Point(source.Right - edge, source.Bottom - edge), corner), Color.White);
    }

    /// <summary>Repeats <paramref name="source"/> across <paramref name="area"/> at its own size,
    /// cutting the last copy in each direction to fit.</summary>
    private static void Tile(SpriteBatch batch, Texture2D texture, Rectangle source, Rectangle area)
    {
        for (var y = area.Y; y < area.Bottom; y += source.Height)
        for (var x = area.X; x < area.Right; x += source.Width)
        {
            var width = Math.Min(source.Width, area.Right - x);
            var height = Math.Min(source.Height, area.Bottom - y);
            batch.Draw(texture, new Rectangle(x, y, width, height),
                new Rectangle(source.X, source.Y, width, height), Color.White);
        }
    }

    /// <summary>
    /// Prints the build's version in the corner the logo leaves free.
    /// </summary>
    /// <remarks>
    /// The title screen is the one place every player passes through, so it is where the number a
    /// bug report will be filed against has to be readable without opening anything.
    /// </remarks>
    private static void DrawTitleVersion(SpriteBatch batch, PixelFont font)
    {
        font.Draw(batch, GameVersion.Display, new Vector2(TitleVersionMargin, TitleVersionTop),
            new Color(150, 160, 160), 1);
    }

    /// <summary>
    /// DEV-UI-019: the "restored by" line and the Refurbished Dinosaurs logo under it, gold under
    /// the pointer. A panel open over the title takes the pointer, so the logo stays grey under it.
    /// </summary>
    private void DrawTitleDinoLogo(SpriteBatch batch)
    {
        if (_restoredBy is not null)
            batch.Draw(_restoredBy, TitleDinoLogo.CaptionBounds, TitleDinoLogo.CaptionColor);
        if (_dinoLogo is null) return;
        batch.Draw(_dinoLogo, TitleDinoLogo.Bounds,
            TitleDinoLogo.Tint(_gameMenuOpen ? null : _hoverPoint));
    }

    /// <summary>Opens the project's site in the player's browser.</summary>
    private void OpenTitleDinoLogoSite()
    {
        try
        {
            using var _ = Process.Start(new ProcessStartInfo(TitleDinoLogo.Url) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            _message = $"OPEN {TitleDinoLogo.Url} IN A BROWSER";
        }
    }

    /// <summary>Draws whatever the last screen left to say, wrapped into the margin.</summary>
    private void DrawTitleMessage(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (string.IsNullOrWhiteSpace(_message)) return;
        var columns = TitleMessage.Width / OriginalFontLayout.CellWidth;
        var lines = HelpTextLayout.Wrap(_message, columns)
            .Take(TitleMessage.Height / OriginalFontLayout.LineHeight)
            .ToArray();
        var panel = new Rectangle(TitleMessage.X - 5, TitleMessage.Y - 5, TitleMessage.Width + 10,
            lines.Length * OriginalFontLayout.LineHeight + 9);
        batch.Draw(pixel, panel, new Color(0, 0, 0, 200));
        for (var row = 0; row < lines.Length; row++)
            font.Draw(batch, lines[row],
                new Vector2(TitleMessage.X, TitleMessage.Y + row * OriginalFontLayout.LineHeight),
                new Color(185, 195, 195), 1);
    }
}

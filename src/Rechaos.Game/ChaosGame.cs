using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed class ChaosGame : Microsoft.Xna.Framework.Game
{
    private static readonly Color[] PlayerColors =
        [Color.Crimson, Color.CornflowerBlue, Color.LimeGreen, Color.Gold, Color.MediumPurple, Color.DarkOrange];
    private readonly GraphicsDeviceManager _graphics;
    private readonly string _assetRoot;
    private SpriteBatch? _batch;
    private Texture2D? _pixel;
    private Texture2D? _background;
    private PixelFont? _font;
    private GameState? _state;
    private KeyboardState _previousKeyboard;

    public ChaosGame(string assetRoot)
    {
        _assetRoot = assetRoot;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 920,
            SynchronizeWithVerticalRetrace = true
        };
        IsMouseVisible = true;
        Window.Title = "Re: Chaos Overlords";
    }

    protected override void LoadContent()
    {
        ValidateAssetPack();
        _batch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
        _font = new PixelFont(_pixel);
        _state = new GameState(BundledOriginalData.Load());

        var backgroundPath = Path.Combine(_assetRoot, "images", "PX00100.bmp");
        if (File.Exists(backgroundPath))
        {
            using var stream = File.OpenRead(backgroundPath);
            _background = Texture2D.FromStream(GraphicsDevice, stream);
        }
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        if (Pressed(keyboard, Keys.Escape)) Exit();
        if (_state is not null)
        {
            if (Pressed(keyboard, Keys.Left) || Pressed(keyboard, Keys.A)) _state.MoveCursor(-1, 0);
            if (Pressed(keyboard, Keys.Right) || Pressed(keyboard, Keys.D)) _state.MoveCursor(1, 0);
            if (Pressed(keyboard, Keys.Up) || Pressed(keyboard, Keys.W)) _state.MoveCursor(0, -1);
            if (Pressed(keyboard, Keys.Down) || Pressed(keyboard, Keys.S)) _state.MoveCursor(0, 1);
            if (Pressed(keyboard, Keys.Enter)) _state.TakeControl();
            if (Pressed(keyboard, Keys.H)) _state.HireGang();
            if (Pressed(keyboard, Keys.Space)) _state.EndTurn();
        }
        _previousKeyboard = keyboard;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(8, 10, 12));
        if (_batch is null || _pixel is null || _font is null || _state is null) return;
        var viewport = GraphicsDevice.Viewport;
        var scale = MathF.Min(viewport.Width / 640f, viewport.Height / 460f);
        var transform = Matrix.CreateScale(scale) * Matrix.CreateTranslation(
            (viewport.Width - 640 * scale) / 2, (viewport.Height - 460 * scale) / 2, 0);
        _batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: transform);
        if (_background is not null)
            _batch.Draw(_background, new Rectangle(0, 0, 640, 460), Color.White);
        else
            _batch.Draw(_pixel, new Rectangle(0, 0, 640, 460), new Color(22, 27, 28));
        DrawBoard(_batch, _pixel, _font, _state);
        _batch.End();
        base.Draw(gameTime);
    }

    private static void DrawBoard(SpriteBatch batch, Texture2D pixel, PixelFont font, GameState state)
    {
        batch.Draw(pixel, new Rectangle(11, 8, 618, 29), new Color(0, 0, 0, 205));
        font.Draw(batch, "CHAOS OVERLORDS", new Vector2(20, 15), Color.Gold, 2);
        font.Draw(batch, $"TURN {state.Turn}   {state.Players[state.CurrentPlayer].Name}   CASH ${state.Players[state.CurrentPlayer].Cash}",
            new Vector2(236, 17), Color.White, 1);

        const int left = 58, top = 51, cellWidth = 65, cellHeight = 42;
        batch.Draw(pixel, new Rectangle(left - 4, top - 4, cellWidth * 8 + 8, cellHeight * 8 + 8), new Color(0, 0, 0, 190));
        for (var index = 0; index < state.Sectors.Length; index++)
        {
            var sector = state.Sectors[index];
            var x = left + index % 8 * cellWidth;
            var y = top + index / 8 * cellHeight;
            var fill = sector.Owner < 0 ? new Color(24, 37, 39, 220) : PlayerColors[sector.Owner] * .68f;
            batch.Draw(pixel, new Rectangle(x + 1, y + 1, cellWidth - 2, cellHeight - 2), fill);
            batch.Draw(pixel, new Rectangle(x + 4, y + 5, cellWidth - 8, 1), new Color(100, 125, 112));
            font.Draw(batch, (index + 1).ToString("00"), new Vector2(x + 5, y + 13), Color.White, 1);
            font.Draw(batch, "$" + sector.Income, new Vector2(x + 30, y + 13), new Color(180, 230, 170), 1);
            if (index == state.Cursor) DrawBorder(batch, pixel, new Rectangle(x, y, cellWidth, cellHeight), Color.Gold, 2);
        }

        var player = state.Players[state.CurrentPlayer];
        batch.Draw(pixel, new Rectangle(11, 397, 618, 52), new Color(0, 0, 0, 220));
        font.Draw(batch, state.Sectors[state.Cursor].Summary, new Vector2(18, 404), Color.White, 1);
        font.Draw(batch, $"GANGS {player.Gangs.Count}/80", new Vector2(18, 420), PlayerColors[state.CurrentPlayer], 1);
        font.Draw(batch, state.Message, new Vector2(116, 420), Color.Gold, 1);
        font.Draw(batch, "ARROWS MOVE  ENTER CONTROL  H HIRE  SPACE END TURN  ESC QUIT", new Vector2(18, 436), new Color(180, 190, 190), 1);
    }

    private static void DrawBorder(SpriteBatch batch, Texture2D pixel, Rectangle rectangle, Color color, int thickness)
    {
        batch.Draw(pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
        batch.Draw(pixel, new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
        batch.Draw(pixel, new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
        batch.Draw(pixel, new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height), color);
    }

    private bool Pressed(KeyboardState current, Keys key) => current.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

    private void ValidateAssetPack()
    {
        var manifestPath = Path.Combine(_assetRoot, "manifest.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Original assets are not installed. Run Rechaos.Extractor with --source pointing at a legal Chaos Overlords installation.", manifestPath);
        var manifest = JsonSerializer.Deserialize<AssetManifest>(File.ReadAllText(manifestPath));
        if (manifest?.FormatVersion != AssetManifest.CurrentFormatVersion)
            throw new InvalidDataException("The asset pack is incompatible. Run the current extractor again.");
    }
}

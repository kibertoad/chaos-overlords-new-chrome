using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;

namespace Rechaos.Game;

public sealed class ChaosGame : Microsoft.Xna.Framework.Game
{
    private static readonly Color[] PlayerColors =
        [Color.Crimson, Color.CornflowerBlue, Color.LimeGreen, Color.Gold, Color.MediumPurple, Color.DarkOrange];
    private readonly GraphicsDeviceManager _graphics;
    private readonly string _assetRoot;
    private readonly string _quickSavePath;
    private readonly string _autoSavePath;
    private readonly string _replayPath;
    private SpriteBatch? _batch;
    private Texture2D? _pixel;
    private Texture2D? _background;
    private PixelFont? _font;
    private MatchState? _state;
    private MatchReplayRecorder? _replay;
    private int _cursor;
    private string _message = "ADVANCE UPKEEP TO BEGIN";
    private KeyboardState _previousKeyboard;

    public ChaosGame(string assetRoot)
    {
        _assetRoot = assetRoot;
        _quickSavePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Rechaos Overlords", "quicksave.rchsave");
        _autoSavePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Rechaos Overlords", "autosave.rchsave");
        _replayPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Rechaos Overlords", "last-match.rchreplay");
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
        _state = CreateInitialMatch(BundledOriginalData.Load());
        _replay = new MatchReplayRecorder(_state);

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
            if (Pressed(keyboard, Keys.Left) || Pressed(keyboard, Keys.A)) MoveCursor(-1, 0);
            if (Pressed(keyboard, Keys.Right) || Pressed(keyboard, Keys.D)) MoveCursor(1, 0);
            if (Pressed(keyboard, Keys.Up) || Pressed(keyboard, Keys.W)) MoveCursor(0, -1);
            if (Pressed(keyboard, Keys.Down) || Pressed(keyboard, Keys.S)) MoveCursor(0, 1);
            if (Pressed(keyboard, Keys.Enter)) QueueBoardCommand();
            if (Pressed(keyboard, Keys.H)) QueueFirstHireOffer();
            if (Pressed(keyboard, Keys.Space)) AdvancePhase();
            if (Pressed(keyboard, Keys.F5)) SaveQuickGame();
            if (Pressed(keyboard, Keys.F9)) LoadQuickGame();
            if (Pressed(keyboard, Keys.F6)) SaveReplay();
            if (Pressed(keyboard, Keys.F10)) LoadReplay();
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

    private void DrawBoard(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        var playerIndex = state.Coordinator.ActivePlayer?.Value ?? 0;
        var player = state.Players[playerIndex];
        batch.Draw(pixel, new Rectangle(11, 8, 618, 29), new Color(0, 0, 0, 205));
        font.Draw(batch, "CHAOS OVERLORDS", new Vector2(20, 15), Color.Gold, 2);
        font.Draw(batch, $"TURN {state.Coordinator.Turn}   {player.Setup.Name}   CASH ${player.Cash}",
            new Vector2(236, 17), Color.White, 1);

        const int left = 58, top = 51, cellWidth = 65, cellHeight = 42;
        batch.Draw(pixel, new Rectangle(left - 4, top - 4, cellWidth * 8 + 8, cellHeight * 8 + 8), new Color(0, 0, 0, 190));
        for (var index = 0; index < state.Sectors.Count; index++)
        {
            var sector = state.Sectors[index];
            var x = left + index % 8 * cellWidth;
            var y = top + index / 8 * cellHeight;
            var fill = sector.Owner is null
                ? new Color(24, 37, 39, 220)
                : PlayerColors[sector.Owner.Value.Value] * .68f;
            batch.Draw(pixel, new Rectangle(x + 1, y + 1, cellWidth - 2, cellHeight - 2), fill);
            batch.Draw(pixel, new Rectangle(x + 4, y + 5, cellWidth - 8, 1), new Color(100, 125, 112));
            font.Draw(batch, (index + 1).ToString("00"), new Vector2(x + 5, y + 13), Color.White, 1);
            font.Draw(batch, "$" + SectorSiteIncome(state, sector), new Vector2(x + 30, y + 13), new Color(180, 230, 170), 1);
            if (index == _cursor) DrawBorder(batch, pixel, new Rectangle(x, y, cellWidth, cellHeight), Color.Gold, 2);
        }

        batch.Draw(pixel, new Rectangle(11, 397, 618, 52), new Color(0, 0, 0, 220));
        font.Draw(batch, SectorSummary(state, state.Sectors[_cursor]), new Vector2(18, 404), Color.White, 1);
        font.Draw(batch, $"GANGS {player.Gangs.Count}/{MatchLimits.GangsPerPlayer}", new Vector2(18, 420), PlayerColors[playerIndex], 1);
        font.Draw(batch, _message, new Vector2(116, 420), Color.Gold, 1);
        font.Draw(batch, "ARROWS ENTER/H/SPACE  F5/F9 SAVE/LOAD  F6/F10 REPLAY", new Vector2(18, 436), new Color(180, 190, 190), 1);
    }

    private void MoveCursor(int dx, int dy)
    {
        var x = Math.Clamp(_cursor % MatchLimits.BoardWidth + dx, 0, MatchLimits.BoardWidth - 1);
        var y = Math.Clamp(_cursor / MatchLimits.BoardWidth + dy, 0, MatchLimits.BoardWidth - 1);
        _cursor = y * MatchLimits.BoardWidth + x;
        _message = $"SECTOR {_cursor + 1}";
    }

    private void QueueBoardCommand()
    {
        if (_state is null || _state.Coordinator.Phase != TurnPhase.Command
            || _state.Coordinator.ActivePlayer is not { } playerId)
        {
            _message = "BOARD COMMANDS REQUIRE THE COMMAND PHASE";
            return;
        }
        var gang = _state.FindPlayer(playerId)!.Gangs.FirstOrDefault(candidate => candidate.IsActive);
        if (gang is null)
        {
            _message = "NO ACTIVE GANG";
            return;
        }
        var command = gang.SectorId == _cursor
            ? new GameCommand(playerId, gang.Id, GangAction.Control, CommandTarget.None)
            : new GameCommand(playerId, gang.Id, GangAction.Move, CommandTarget.Sector(_cursor));
        var result = _replay!.Submit(command);
        _message = result.Accepted ? $"{command.Action.ToString().ToUpperInvariant()} QUEUED" : result.Validation.Message.ToUpperInvariant();
    }

    private void QueueFirstHireOffer()
    {
        if (_state is null || _state.Coordinator.Phase != TurnPhase.Hire
            || _state.Coordinator.ActivePlayer is not { } playerId)
        {
            _message = "HIRING REQUIRES THE HIRE PHASE";
            return;
        }
        var player = _state.FindPlayer(playerId)!;
        if (player.HirePool.Count == 0)
        {
            _message = "NO HIRE OFFER AVAILABLE";
            return;
        }
        var offer = player.HirePool[0];
        var result = _replay!.QueueHire(playerId, offer, _cursor);
        _message = result.Accepted ? "HIRE QUEUED" : result.Validation.Message.ToUpperInvariant();
    }

    private void AdvancePhase()
    {
        if (_state is null) return;
        if (_state.Outcome is not null)
        {
            _message = "MATCH COMPLETE";
            return;
        }
        var completedTurn = _state.Coordinator.Phase == TurnPhase.PlayerElimination;
        var transition = _state.Coordinator.Phase switch
        {
            TurnPhase.Upkeep => _replay!.FinishUpkeep(),
            TurnPhase.Command => _replay!.FinishCommand(_state.Coordinator.ActivePlayer!.Value),
            TurnPhase.Execution => _replay!.FinishExecutionPhase(),
            TurnPhase.Hire => _replay!.FinishHire(_state.Coordinator.ActivePlayer!.Value),
            TurnPhase.PlayerElimination => _replay!.FinishPlayerElimination(),
            _ => throw new InvalidOperationException("Unknown turn phase.")
        };
        _message = transition.ExecutionPhase is { } execution
            ? execution.ToString().ToUpperInvariant()
            : transition.Phase.ToString().ToUpperInvariant();
        if (completedTurn)
        {
            try
            {
                NativeSaveStore.SaveAtomic(_autoSavePath, _state);
                _message += "  AUTOSAVED";
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _message += "  AUTOSAVE FAILED";
            }
        }
    }

    private void SaveQuickGame()
    {
        if (_state is null) return;
        try
        {
            NativeSaveStore.SaveAtomic(_quickSavePath, _state);
            _message = "GAME SAVED";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _message = "SAVE FAILED";
        }
    }

    private void LoadQuickGame()
    {
        if (_state is null) return;
        try
        {
            var result = NativeSaveStore.LoadRecoveringBackup(_quickSavePath, _state.Definitions);
            _state = result.State;
            _replay = new MatchReplayRecorder(_state);
            _cursor = Math.Clamp(_cursor, 0, _state.Sectors.Count - 1);
            _message = result.RecoveredFromBackup ? "BACKUP GAME LOADED" : "GAME LOADED";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _message = "LOAD FAILED";
        }
    }

    private void SaveReplay()
    {
        if (_replay is null) return;
        try
        {
            MatchReplayStore.SaveAtomic(_replayPath, _replay);
            _message = "REPLAY SAVED";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _message = "REPLAY SAVE FAILED";
        }
    }

    private void LoadReplay()
    {
        if (_state is null) return;
        try
        {
            _state = MatchReplayStore.LoadAndReplay(_replayPath, _state.Definitions);
            _replay = new MatchReplayRecorder(_state);
            _cursor = Math.Clamp(_cursor, 0, _state.Sectors.Count - 1);
            _message = "REPLAY VERIFIED";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _message = "REPLAY FAILED";
        }
    }

    private static int SectorSiteIncome(MatchState state, MatchSectorState sector) => sector.Income;

    private static string SectorSummary(MatchState state, MatchSectorState sector) =>
        $"SECTOR {sector.Id + 1}  TOL {sector.Tolerance}  "
        + string.Join(", ", sector.Sites.Select(site =>
            state.Definitions.Sites.Single(definition => definition.Id == site.DefinitionId).Name));

    private static MatchState CreateInitialMatch(OriginalData data)
    {
        var playerSetup = new MatchPlayerSetup(new PlayerId(0), "PLAYER 1", PlayerController.Human);
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, [playerSetup]);
        return OriginalMatchFactory.Create(data, setup);
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

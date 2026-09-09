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
    private static readonly GameDuration[] Durations = Enum.GetValues<GameDuration>();
    private static readonly Rectangle TitleNewGame = new(220, 250, 200, 34);
    private static readonly Rectangle TitleLoadGame = new(220, 294, 200, 34);
    private static readonly Rectangle TitleQuit = new(220, 338, 200, 34);
    private static readonly Rectangle SetupScenarioPrevious = new(92, 134, 42, 30);
    private static readonly Rectangle SetupScenarioNext = new(506, 134, 42, 30);
    private static readonly Rectangle SetupDurationPrevious = new(92, 204, 42, 30);
    private static readonly Rectangle SetupDurationNext = new(506, 204, 42, 30);
    private static readonly Rectangle SetupPlayersPrevious = new(92, 274, 42, 30);
    private static readonly Rectangle SetupPlayersNext = new(506, 274, 42, 30);
    private static readonly Rectangle SetupStart = new(220, 352, 200, 34);
    private static readonly Rectangle SetupBack = new(220, 396, 200, 28);
    private static readonly Rectangle CityAction = new(430, 415, 86, 24);
    private static readonly Rectangle CityAdvance = new(524, 415, 96, 24);
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
    private OriginalData? _definitions;
    private readonly ScreenRouter _screens = new();
    private ScenarioId _selectedScenario = ScenarioId.Greed;
    private GameDuration _selectedDuration = GameDuration.SixMonths;
    private int _selectedPlayerCount = 2;
    private int _cursor;
    private string _message = "SELECT NEW GAME";
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;

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
        _definitions = BundledOriginalData.Load();

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
        var mouse = Mouse.GetState();
        if (Pressed(keyboard, Keys.Escape) && !_screens.Back()) Exit();
        switch (_screens.Current)
        {
            case ClientScreen.Title:
                UpdateTitle(keyboard);
                break;
            case ClientScreen.Setup:
                UpdateSetup(keyboard);
                break;
            case ClientScreen.City:
                UpdateCity(keyboard);
                break;
        }
        if (mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released
            && VirtualInput.TryMap(GraphicsDevice.Viewport, mouse.Position, out var virtualPoint))
            HandleClick(virtualPoint);
        _previousKeyboard = keyboard;
        _previousMouse = mouse;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(8, 10, 12));
        if (_batch is null || _pixel is null || _font is null) return;
        var viewport = GraphicsDevice.Viewport;
        var transform = VirtualInput.Transform(viewport);
        _batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: transform);
        if (_background is not null)
            _batch.Draw(_background, new Rectangle(0, 0, 640, 460), Color.White);
        else
            _batch.Draw(_pixel, new Rectangle(0, 0, 640, 460), new Color(22, 27, 28));
        switch (_screens.Current)
        {
            case ClientScreen.Title:
                DrawTitle(_batch, _pixel, _font);
                break;
            case ClientScreen.Setup:
                DrawSetup(_batch, _pixel, _font);
                break;
            case ClientScreen.City when _state is not null:
                DrawBoard(_batch, _pixel, _font, _state);
                break;
        }
        _batch.End();
        base.Draw(gameTime);
    }

    private void UpdateTitle(KeyboardState keyboard)
    {
        if (Pressed(keyboard, Keys.Enter)) _screens.Show(ClientScreen.Setup);
        if (Pressed(keyboard, Keys.F9)) LoadQuickGame();
    }

    private void UpdateSetup(KeyboardState keyboard)
    {
        if (Pressed(keyboard, Keys.Left)) ChangeScenario(-1);
        if (Pressed(keyboard, Keys.Right)) ChangeScenario(1);
        if (Pressed(keyboard, Keys.Up)) ChangeDuration(1);
        if (Pressed(keyboard, Keys.Down)) ChangeDuration(-1);
        if (Pressed(keyboard, Keys.OemMinus)) ChangePlayerCount(-1);
        if (Pressed(keyboard, Keys.OemPlus)) ChangePlayerCount(1);
        if (Pressed(keyboard, Keys.Enter)) StartMatch();
    }

    private void UpdateCity(KeyboardState keyboard)
    {
        if (_state is null) return;
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

    private void HandleClick(Point point)
    {
        switch (_screens.Current)
        {
            case ClientScreen.Title:
                if (TitleNewGame.Contains(point)) _screens.Show(ClientScreen.Setup);
                else if (TitleLoadGame.Contains(point)) LoadQuickGame();
                else if (TitleQuit.Contains(point)) Exit();
                break;
            case ClientScreen.Setup:
                if (SetupScenarioPrevious.Contains(point)) ChangeScenario(-1);
                else if (SetupScenarioNext.Contains(point)) ChangeScenario(1);
                else if (SetupDurationPrevious.Contains(point)) ChangeDuration(-1);
                else if (SetupDurationNext.Contains(point)) ChangeDuration(1);
                else if (SetupPlayersPrevious.Contains(point)) ChangePlayerCount(-1);
                else if (SetupPlayersNext.Contains(point)) ChangePlayerCount(1);
                else if (SetupStart.Contains(point)) StartMatch();
                else if (SetupBack.Contains(point)) _screens.Show(ClientScreen.Title);
                break;
            case ClientScreen.City:
                HandleCityClick(point);
                break;
        }
    }

    private void HandleCityClick(Point point)
    {
        const int left = 58, top = 51, cellWidth = 65, cellHeight = 42;
        if (point.X >= left && point.X < left + cellWidth * 8
            && point.Y >= top && point.Y < top + cellHeight * 8)
        {
            var selected = (point.Y - top) / cellHeight * 8 + (point.X - left) / cellWidth;
            if (_cursor == selected) QueueBoardCommand();
            else
            {
                _cursor = selected;
                _message = $"SECTOR {_cursor + 1}";
            }
        }
        else if (CityAction.Contains(point))
        {
            if (_state?.Coordinator.Phase == TurnPhase.Hire) QueueFirstHireOffer();
            else QueueBoardCommand();
        }
        else if (CityAdvance.Contains(point)) AdvancePhase();
    }

    private void ChangeScenario(int delta)
    {
        var count = ScenarioCatalog.All.Count;
        _selectedScenario = ScenarioCatalog.All[Mod((int)_selectedScenario + delta, count)].Id;
    }

    private void ChangeDuration(int delta)
    {
        _selectedDuration = Durations[Mod(Array.IndexOf(Durations, _selectedDuration) + delta, Durations.Length)];
    }

    private void ChangePlayerCount(int delta) =>
        _selectedPlayerCount = Math.Clamp(_selectedPlayerCount + delta, 1, MatchLimits.PlayerCount);

    private void StartMatch()
    {
        if (_definitions is null) return;
        var players = Enumerable.Range(0, _selectedPlayerCount)
            .Select(index => new MatchPlayerSetup(
                new PlayerId(index), $"PLAYER {index + 1}",
                PlayerController.Human))
            .ToArray();
        var setup = new MatchSetup(_selectedScenario, _selectedDuration, Environment.TickCount, players);
        _state = OriginalMatchFactory.Create(_definitions, setup);
        _replay = new MatchReplayRecorder(_state);
        _cursor = _state.Players[0].Gangs[0].SectorId;
        _message = "ADVANCE UPKEEP TO BEGIN";
        _screens.Show(ClientScreen.City);
    }

    private void DrawTitle(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        batch.Draw(pixel, new Rectangle(92, 72, 456, 112), new Color(0, 0, 0, 210));
        DrawCentered(font, batch, "CHAOS OVERLORDS", 103, Color.Gold, 3);
        DrawCentered(font, batch, "A CLEAN ROOM REIMPLEMENTATION", 151, Color.White, 1);
        DrawButton(batch, pixel, font, TitleNewGame, "NEW GAME", true);
        DrawButton(batch, pixel, font, TitleLoadGame, "LOAD GAME", true);
        DrawButton(batch, pixel, font, TitleQuit, "QUIT", true);
        DrawCentered(font, batch, _message, 410, new Color(185, 195, 195), 1);
    }

    private void DrawSetup(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        batch.Draw(pixel, new Rectangle(70, 52, 500, 384), new Color(0, 0, 0, 220));
        DrawCentered(font, batch, "NEW GAME SETUP", 72, Color.Gold, 2);
        DrawCentered(font, batch, "SCENARIO", 111, new Color(170, 190, 190), 1);
        DrawSelector(batch, pixel, font, SetupScenarioPrevious, SetupScenarioNext,
            ScenarioCatalog.Get(_selectedScenario).Name, 134);
        DrawCentered(font, batch, ScenarioCatalog.Get(_selectedScenario).Objective, 174, Color.White, 1);
        DrawCentered(font, batch, "DURATION", 191, new Color(170, 190, 190), 1);
        DrawSelector(batch, pixel, font, SetupDurationPrevious, SetupDurationNext,
            DurationLabel(_selectedDuration), 204);
        DrawCentered(font, batch, "PLAYERS", 261, new Color(170, 190, 190), 1);
        DrawSelector(batch, pixel, font, SetupPlayersPrevious, SetupPlayersNext,
            _selectedPlayerCount.ToString(), 274);
        DrawCentered(font, batch, "LOCAL HOT SEAT PLAYERS", 320, Color.White, 1);
        DrawButton(batch, pixel, font, SetupStart, "START", true);
        DrawButton(batch, pixel, font, SetupBack, "BACK", false);
    }

    private void DrawSelector(
        SpriteBatch batch, Texture2D pixel, PixelFont font,
        Rectangle previous, Rectangle next, string value, int y)
    {
        DrawButton(batch, pixel, font, previous, "-", false);
        DrawButton(batch, pixel, font, next, "+", false);
        batch.Draw(pixel, new Rectangle(144, y, 352, 30), new Color(24, 37, 39, 235));
        DrawBorder(batch, pixel, new Rectangle(144, y, 352, 30), new Color(100, 125, 112), 1);
        DrawCentered(font, batch, value, y + 11, Color.White, 1);
    }

    private static string DurationLabel(GameDuration duration) => duration switch
    {
        GameDuration.SixMonths => "6 MONTHS",
        GameDuration.OneYear => "1 YEAR",
        GameDuration.TwoYears => "2 YEARS",
        GameDuration.FourYears => "4 YEARS",
        _ => throw new ArgumentOutOfRangeException(nameof(duration))
    };

    private static int Mod(int value, int divisor) => (value % divisor + divisor) % divisor;

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

        batch.Draw(pixel, new Rectangle(11, 389, 618, 60), new Color(0, 0, 0, 220));
        font.Draw(batch, SectorSummary(state, state.Sectors[_cursor]), new Vector2(18, 395), Color.White, 1);
        font.Draw(batch, $"GANGS {player.Gangs.Count}/{MatchLimits.GangsPerPlayer}", new Vector2(18, 410), PlayerColors[playerIndex], 1);
        font.Draw(batch, _message, new Vector2(116, 410), Color.Gold, 1);
        DrawButton(batch, pixel, font, CityAction,
            state.Coordinator.Phase == TurnPhase.Hire ? "HIRE" : "ACTION", false);
        DrawButton(batch, pixel, font, CityAdvance, "ADVANCE", false);
        font.Draw(batch, "ARROWS ENTER/H/SPACE  F5/F9 SAVE  F6/F10 REPLAY", new Vector2(18, 439), new Color(180, 190, 190), 1);
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
        if (_definitions is null) return;
        try
        {
            var result = NativeSaveStore.LoadRecoveringBackup(_quickSavePath, _definitions);
            _state = result.State;
            _replay = new MatchReplayRecorder(_state);
            _cursor = Math.Clamp(_cursor, 0, _state.Sectors.Count - 1);
            _message = result.RecoveredFromBackup ? "BACKUP GAME LOADED" : "GAME LOADED";
            _screens.Show(ClientScreen.City);
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

    private static void DrawCentered(
        PixelFont font, SpriteBatch batch, string text, int y, Color color, int scale)
    {
        var width = text.Length * 6 * scale;
        font.Draw(batch, text, new Vector2((VirtualInput.Width - width) / 2, y), color, scale);
    }

    private static void DrawButton(
        SpriteBatch batch, Texture2D pixel, PixelFont font,
        Rectangle rectangle, string text, bool prominent)
    {
        var fill = prominent ? new Color(72, 54, 18, 235) : new Color(24, 37, 39, 235);
        var border = prominent ? Color.Gold : new Color(100, 125, 112);
        batch.Draw(pixel, rectangle, fill);
        DrawBorder(batch, pixel, rectangle, border, prominent ? 2 : 1);
        var x = rectangle.X + (rectangle.Width - text.Length * 6) / 2;
        var y = rectangle.Y + (rectangle.Height - 7) / 2;
        font.Draw(batch, text, new Vector2(x, y), Color.White, 1);
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

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
        [Color.Red, Color.LimeGreen, Color.Blue, Color.Yellow, Color.Magenta, Color.Cyan];
    private static readonly GameDuration[] Durations = Enum.GetValues<GameDuration>();
    private static readonly Rectangle TitleNewGame = new(220, 292, 200, 34);
    private static readonly Rectangle TitleLoadGame = new(220, 334, 200, 34);
    private static readonly Rectangle TitleQuit = new(220, 376, 200, 34);
    private static readonly Rectangle[] SetupScenarios =
    [
        new(80, 102, 108, 31), new(192, 102, 108, 31),
        new(80, 137, 108, 31), new(192, 137, 108, 31),
        new(80, 171, 108, 31), new(192, 171, 108, 31),
        new(80, 206, 108, 31), new(192, 206, 108, 31),
        new(80, 241, 108, 30), new(192, 241, 108, 30)
    ];
    private static readonly Rectangle[] SetupDurations =
    [new(80, 282, 50, 24), new(136, 282, 50, 24), new(192, 282, 50, 24), new(248, 282, 52, 24)];
    private static readonly Rectangle SetupPlayersAdd = new(370, 326, 92, 30);
    private static readonly Rectangle SetupPlayersRemove = new(466, 326, 96, 30);
    private static readonly Rectangle SetupStart = new(370, 374, 92, 50);
    private static readonly Rectangle SetupBack = new(466, 374, 96, 50);
    private static readonly Rectangle CityAction = new(430, 415, 86, 24);
    private static readonly Rectangle CityAdvance = new(524, 415, 96, 24);
    private static readonly Rectangle CityEvents = new(492, 124, 50, 51);
    private static readonly Rectangle CityCombatSummary = new(492, 176, 50, 49);
    private static readonly Rectangle CityFinance = new(548, 176, 50, 49);
    private static readonly Rectangle CityGangs = new(492, 226, 50, 17);
    private static readonly Rectangle CityHire = new(492, 260, 50, 17);
    private static readonly Rectangle CitySector = new(548, 226, 50, 17);
    private static readonly Rectangle CityRanking = new(548, 243, 50, 17);
    private static readonly Rectangle CitySearch = new(548, 260, 50, 17);
    private static readonly Rectangle EventsDismiss = new(218, 414, 96, 28);
    private static readonly Rectangle EventsBack = new(322, 414, 96, 28);
    private static readonly Rectangle CommandsQueue = new(218, 414, 96, 28);
    private static readonly Rectangle CommandsBack = new(322, 414, 96, 28);
    private static readonly Rectangle HireQueue = new(114, 414, 96, 28);
    private static readonly Rectangle HireSnub = new(218, 414, 96, 28);
    private static readonly Rectangle HireBack = new(322, 414, 96, 28);
    private static readonly Rectangle ManagementBack = new(322, 414, 96, 28);
    private static readonly Rectangle ItemsResearch = new(114, 414, 96, 28);
    private static readonly Rectangle ItemsEquip = new(218, 414, 96, 28);
    private static readonly Rectangle ItemsSell = new(322, 414, 96, 28);
    private static readonly Rectangle ItemsBack = new(426, 414, 96, 28);
    private static readonly Rectangle EndgameDone = new(320, 404, 104, 54);
    private static readonly Rectangle HandoffReady = new(266, 246, 108, 66);
    private readonly GraphicsDeviceManager _graphics;
    private readonly string _assetRoot;
    private readonly string _quickSavePath;
    private readonly string _autoSavePath;
    private readonly string _replayPath;
    private SpriteBatch? _batch;
    private Texture2D? _pixel;
    private Texture2D? _titleBackground;
    private Texture2D? _setupBackground;
    private Texture2D? _cityBackground;
    private readonly Texture2D?[] _cityOwnershipLayers = new Texture2D?[MatchLimits.PlayerCount + 1];
    private Texture2D? _endgameBackground;
    private Texture2D? _handoffPanel;
    private Texture2D? _gangInfoBackground;
    private Texture2D? _sitePortraits;
    private Texture2D? _gangPortraits;
    private PixelFont? _font;
    private MatchState? _state;
    private MatchReplayRecorder? _replay;
    private OriginalData? _definitions;
    private readonly ScreenRouter _screens = new();
    private ScenarioId _selectedScenario = ScenarioId.Greed;
    private GameDuration _selectedDuration = GameDuration.SixMonths;
    private int _selectedPlayerCount = 2;
    private int _cursor;
    private int _selectedGangIndex;
    private IReadOnlyList<GameCommand> _commandOptions = [];
    private int _commandCursor;
    private int _hireCursor;
    private int _itemCursor;
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

        _titleBackground = LoadTexture("PX00130.bmp");
        _setupBackground = LoadTexture("PX00143.bmp");
        _cityBackground = LoadTexture("PX00128.bmp");
        for (var index = 0; index < _cityOwnershipLayers.Length; index++)
            _cityOwnershipLayers[index] = LoadTexture($"PX1000{index}.bmp");
        _endgameBackground = LoadTexture("PX00200.bmp");
        _handoffPanel = LoadTexture("PX00132.bmp");
        _gangInfoBackground = LoadTexture("PX05000.bmp");
        _sitePortraits = LoadTexture("PX02000.bmp");
        _gangPortraits = LoadTexture("PX03000.bmp");
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
            case ClientScreen.Endgame:
                if (Pressed(keyboard, Keys.Enter)) _screens.Show(ClientScreen.Title);
                break;
            case ClientScreen.Handoff:
                if (Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Space))
                    _screens.Show(ClientScreen.City);
                break;
            case ClientScreen.Events:
                if (Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Delete)) DismissNotification();
                if (Pressed(keyboard, Keys.Back)) _screens.Show(ClientScreen.City);
                break;
            case ClientScreen.Commands:
                if (Pressed(keyboard, Keys.Up)) MoveCommandCursor(-1);
                if (Pressed(keyboard, Keys.Down)) MoveCommandCursor(1);
                if (Pressed(keyboard, Keys.Enter)) SubmitSelectedCommand();
                if (Pressed(keyboard, Keys.Back)) _screens.Show(ClientScreen.City);
                break;
            case ClientScreen.Hire:
                if (Pressed(keyboard, Keys.Up)) MoveHireCursor(-1);
                if (Pressed(keyboard, Keys.Down)) MoveHireCursor(1);
                if (Pressed(keyboard, Keys.Enter)) QueueSelectedHireOffer();
                if (Pressed(keyboard, Keys.S)) SnubSelectedHireOffer();
                if (Pressed(keyboard, Keys.Back)) _screens.Show(ClientScreen.City);
                break;
            case ClientScreen.Sector:
                if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
                    _screens.Show(ClientScreen.City);
                break;
            case ClientScreen.Gang:
                if (Pressed(keyboard, Keys.Left) || Pressed(keyboard, Keys.Up)) CycleGang(-1);
                if (Pressed(keyboard, Keys.Right) || Pressed(keyboard, Keys.Down)) CycleGang(1);
                if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
                    _screens.Show(ClientScreen.City);
                break;
            case ClientScreen.Finance:
            case ClientScreen.Ranking:
                if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
                    _screens.Show(ClientScreen.City);
                break;
            case ClientScreen.Items:
                if (Pressed(keyboard, Keys.Up)) MoveItemCursor(-1);
                if (Pressed(keyboard, Keys.Down)) MoveItemCursor(1);
                if (Pressed(keyboard, Keys.Left) || Pressed(keyboard, Keys.G)) CycleGang(-1);
                if (Pressed(keyboard, Keys.Right)) CycleGang(1);
                if (Pressed(keyboard, Keys.R)) QueueItemCommand(GangAction.Research);
                if (Pressed(keyboard, Keys.E)) QueueItemCommand(GangAction.Equip);
                if (Pressed(keyboard, Keys.S)) QueueItemCommand(GangAction.Sell);
                if (Pressed(keyboard, Keys.Back)) _screens.Show(ClientScreen.City);
                break;
            case ClientScreen.CombatSummary:
            case ClientScreen.Search:
                if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
                    _screens.Show(ClientScreen.City);
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
        _batch.Draw(_pixel, new Rectangle(0, 0, 640, 460), new Color(8, 10, 12));
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
            case ClientScreen.Endgame when _state?.Outcome is not null:
                DrawEndgame(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Handoff when _state is not null:
                DrawHandoff(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Events when _state is not null:
                DrawEvents(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Commands when _state is not null:
                DrawCommands(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Hire when _state is not null:
                DrawHire(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Sector when _state is not null:
                DrawSectorDetails(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Gang when _state is not null:
                DrawGangDetails(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Finance when _state is not null:
                DrawFinance(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Ranking when _state is not null:
                DrawRanking(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Items when _state is not null:
                DrawItems(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.CombatSummary when _state is not null:
                DrawCombatSummary(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Search when _state is not null:
                DrawSearch(_batch, _pixel, _font, _state);
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
        if (Pressed(keyboard, Keys.C)) OpenCommands();
        if (Pressed(keyboard, Keys.G)) CycleGang(1);
        if (Pressed(keyboard, Keys.I)) _screens.Show(ClientScreen.Sector);
        if (Pressed(keyboard, Keys.F)) _screens.Show(ClientScreen.Finance);
        if (Pressed(keyboard, Keys.R)) _screens.Show(ClientScreen.Ranking);
        if (Pressed(keyboard, Keys.T)) OpenItems();
        if (Pressed(keyboard, Keys.B)) _screens.Show(ClientScreen.CombatSummary);
        if (Pressed(keyboard, Keys.X)) _screens.Show(ClientScreen.Search);
        if (Pressed(keyboard, Keys.H)) OpenHire();
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
                var scenario = Array.FindIndex(SetupScenarios, rectangle => rectangle.Contains(point));
                var duration = Array.FindIndex(SetupDurations, rectangle => rectangle.Contains(point));
                if (scenario >= 0) _selectedScenario = (ScenarioId)scenario;
                else if (duration >= 0) _selectedDuration = Durations[duration];
                else if (SetupPlayersAdd.Contains(point)) ChangePlayerCount(1);
                else if (SetupPlayersRemove.Contains(point)) ChangePlayerCount(-1);
                else if (SetupStart.Contains(point)) StartMatch();
                else if (SetupBack.Contains(point)) _screens.Show(ClientScreen.Title);
                break;
            case ClientScreen.City:
                HandleCityClick(point);
                break;
            case ClientScreen.Endgame:
                if (EndgameDone.Contains(point)) _screens.Show(ClientScreen.Title);
                break;
            case ClientScreen.Handoff:
                if (HandoffReady.Contains(point)) _screens.Show(ClientScreen.City);
                break;
            case ClientScreen.Events:
                if (EventsDismiss.Contains(point)) DismissNotification();
                else if (EventsBack.Contains(point)) _screens.Show(ClientScreen.City);
                break;
            case ClientScreen.Commands:
                HandleCommandsClick(point);
                break;
            case ClientScreen.Hire:
                HandleHireClick(point);
                break;
            case ClientScreen.Sector:
            case ClientScreen.Gang:
            case ClientScreen.Finance:
            case ClientScreen.Ranking:
            case ClientScreen.CombatSummary:
            case ClientScreen.Search:
                if (ManagementBack.Contains(point)) _screens.Show(ClientScreen.City);
                break;
            case ClientScreen.Items:
                HandleItemsClick(point);
                break;
        }
    }

    private void HandleCityClick(Point point)
    {
        if (CityMapLayout.TrySectorAt(point, out var selected))
        {
            if (_cursor == selected) QueueBoardCommand();
            else
            {
                _cursor = selected;
                _message = $"SECTOR {_cursor + 1}";
            }
        }
        else if (CityAction.Contains(point))
        {
            if (_state?.Coordinator.Phase == TurnPhase.Hire) OpenHire();
            else OpenCommands();
        }
        else if (CityAdvance.Contains(point)) AdvancePhase();
        else if (CityEvents.Contains(point)) _screens.Show(ClientScreen.Events);
        else if (CityCombatSummary.Contains(point)) _screens.Show(ClientScreen.CombatSummary);
        else if (CityFinance.Contains(point)) _screens.Show(ClientScreen.Finance);
        else if (CityGangs.Contains(point)) _screens.Show(ClientScreen.Gang);
        else if (CityHire.Contains(point)) OpenHire();
        else if (CitySector.Contains(point)) _screens.Show(ClientScreen.Sector);
        else if (CityRanking.Contains(point)) _screens.Show(ClientScreen.Ranking);
        else if (CitySearch.Contains(point)) _screens.Show(ClientScreen.Search);
    }

    private void OpenCommands()
    {
        if (_state is null || _state.Coordinator.Phase != TurnPhase.Command
            || _state.Coordinator.ActivePlayer is not { } playerId)
        {
            _message = "COMMAND PICKER REQUIRES THE COMMAND PHASE";
            return;
        }
        var gang = SelectedGang(_state.FindPlayer(playerId)!);
        if (gang is null)
        {
            _message = "NO ACTIVE GANG";
            return;
        }
        _commandOptions = CommandOptionCatalog.LegalCommands(_state, playerId, gang.Id);
        _commandCursor = 0;
        _screens.Show(ClientScreen.Commands);
    }

    private void OpenItems()
    {
        if (_state is null) return;
        var items = RealItems(_state);
        _itemCursor = Math.Clamp(_itemCursor, 0, Math.Max(0, items.Length - 1));
        _screens.Show(ClientScreen.Items);
    }

    private void MoveItemCursor(int delta)
    {
        if (_state is null) return;
        var count = RealItems(_state).Length;
        if (count > 0) _itemCursor = Mod(_itemCursor + delta, count);
    }

    private void HandleItemsClick(Point point)
    {
        if (_state is null) return;
        if (point.X is >= 14 and < 330 && point.Y is >= 108 and < 396)
        {
            var items = RealItems(_state);
            var first = Math.Max(0, _itemCursor - 8);
            var index = first + (point.Y - 108) / 16;
            if (index < items.Length) _itemCursor = index;
        }
        else if (ItemsResearch.Contains(point)) QueueItemCommand(GangAction.Research);
        else if (ItemsEquip.Contains(point)) QueueItemCommand(GangAction.Equip);
        else if (ItemsSell.Contains(point)) QueueItemCommand(GangAction.Sell);
        else if (ItemsBack.Contains(point)) _screens.Show(ClientScreen.City);
    }

    private void QueueItemCommand(GangAction action)
    {
        if (_state is null || _replay is null) return;
        var playerId = _state.Coordinator.ActivePlayer ?? new PlayerId(0);
        var gang = SelectedGang(_state.FindPlayer(playerId)!);
        var items = RealItems(_state);
        if (gang is null || items.Length == 0)
        {
            _message = "NO ACTIVE GANG OR ITEM";
            return;
        }
        var command = new GameCommand(playerId, gang.Id, action, CommandTarget.Item(items[_itemCursor].Id));
        var result = _replay.Submit(command);
        _message = result.Accepted
            ? $"{action.ToString().ToUpperInvariant()} QUEUED"
            : result.Validation.Message.ToUpperInvariant();
        if (result.Accepted) _screens.Show(ClientScreen.City);
    }

    private void MoveCommandCursor(int delta)
    {
        if (_commandOptions.Count == 0) return;
        _commandCursor = Mod(_commandCursor + delta, _commandOptions.Count);
    }

    private void HandleCommandsClick(Point point)
    {
        if (point.X is >= 14 and < 418 && point.Y is >= 102 and < 390)
        {
            var first = Math.Max(0, _commandCursor - 8);
            var index = first + (point.Y - 102) / 16;
            if (index < _commandOptions.Count) _commandCursor = index;
        }
        else if (CommandsQueue.Contains(point)) SubmitSelectedCommand();
        else if (CommandsBack.Contains(point)) _screens.Show(ClientScreen.City);
    }

    private void SubmitSelectedCommand()
    {
        if (_commandOptions.Count == 0 || _replay is null) return;
        var command = _commandOptions[_commandCursor];
        var result = _replay.Submit(command);
        _message = result.Accepted
            ? $"{command.Action.ToString().ToUpperInvariant()} QUEUED"
            : result.Validation.Message.ToUpperInvariant();
        _screens.Show(ClientScreen.City);
    }

    private void CycleGang(int delta)
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId) return;
        var gangs = _state.FindPlayer(playerId)!.Gangs.Where(gang => gang.IsActive).ToArray();
        if (gangs.Length == 0) return;
        _selectedGangIndex = Mod(_selectedGangIndex + delta, gangs.Length);
        _cursor = gangs[_selectedGangIndex].SectorId;
        _message = $"GANG {gangs[_selectedGangIndex].Id.Value}";
    }

    private MatchGangState? SelectedGang(MatchPlayerState player)
    {
        var gangs = player.Gangs.Where(gang => gang.IsActive).ToArray();
        if (gangs.Length == 0) return null;
        _selectedGangIndex = Math.Clamp(_selectedGangIndex, 0, gangs.Length - 1);
        return gangs[_selectedGangIndex];
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
        _selectedGangIndex = 0;
        _message = "ADVANCE UPKEEP TO BEGIN";
        _screens.Show(ClientScreen.City);
    }

    private void DrawTitle(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (_titleBackground is not null)
            batch.Draw(_titleBackground, new Rectangle(0, 0, 640, 460), Color.White);
        else
        {
            batch.Draw(pixel, new Rectangle(92, 72, 456, 112), new Color(0, 0, 0, 210));
            DrawCentered(font, batch, "CHAOS OVERLORDS", 103, Color.Gold, 3);
        }
        DrawButton(batch, pixel, font, TitleNewGame, "NEW GAME", true);
        DrawButton(batch, pixel, font, TitleLoadGame, "LOAD GAME", true);
        DrawButton(batch, pixel, font, TitleQuit, "QUIT", true);
        DrawCentered(font, batch, _message, 410, new Color(185, 195, 195), 1);
    }

    private void DrawSetup(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (_setupBackground is not null)
            batch.Draw(_setupBackground, new Rectangle(0, 0, 640, 460), Color.White);
        else
            batch.Draw(pixel, new Rectangle(70, 52, 500, 384), new Color(0, 0, 0, 220));
        DrawBorder(batch, pixel, SetupScenarios[(int)_selectedScenario], Color.Gold, 2);
        DrawBorder(batch, pixel, SetupDurations[Array.IndexOf(Durations, _selectedDuration)], Color.Gold, 2);
        font.Draw(batch, $"PLAYERS {_selectedPlayerCount}", new Vector2(376, 306), Color.White, 1);
        for (var index = 0; index < _selectedPlayerCount; index++)
        {
            var column = index % 2;
            var row = index / 2;
            var x = 374 + column * 96;
            var y = 130 + row * 64;
            font.Draw(batch, $"P{index + 1}", new Vector2(x, y), PlayerColors[index], 1);
            font.Draw(batch, "HUMAN", new Vector2(x, y + 12), Color.White, 1);
        }
    }

    private static int Mod(int value, int divisor) => (value % divisor + divisor) % divisor;

    private void DrawBoard(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        if (_cityBackground is not null)
            batch.Draw(_cityBackground, new Rectangle(0, 0, 640, 460), Color.White);
        var playerIndex = state.Coordinator.ActivePlayer?.Value ?? 0;
        var player = state.Players[playerIndex];
        var selectedGang = SelectedGang(player);
        for (var index = 0; index < state.Sectors.Count; index++)
        {
            var sector = state.Sectors[index];
            var destination = CityMapLayout.Destination(index);
            var layer = _cityOwnershipLayers[CityMapLayout.OwnershipSheet(sector.Owner)];
            if (layer is not null)
                batch.Draw(layer, destination, CityMapLayout.Source(index), Color.White);
            else
                batch.Draw(pixel, destination, sector.Owner is { } owner
                    ? PlayerColors[owner.Value] * .68f
                    : new Color(24, 37, 39));
            if (index == _cursor) DrawBorder(batch, pixel, destination, Color.Gold, 2);
        }

        var selectedSector = state.Sectors[_cursor];
        batch.Draw(pixel, new Rectangle(474, 5, 114, 108), new Color(0, 0, 0, 205));
        font.Draw(batch, player.Setup.Name, new Vector2(480, 12), PlayerColors[playerIndex], 1);
        font.Draw(batch, $"T{state.Coordinator.Turn} {state.Coordinator.Phase.ToString().ToUpperInvariant()}",
            new Vector2(480, 26), Color.Lime, 1);
        font.Draw(batch, $"CASH ${player.Cash}", new Vector2(480, 48), Color.Lime, 1);
        font.Draw(batch, $"SECTOR {_cursor + 1}", new Vector2(480, 64), Color.Lime, 1);
        font.Draw(batch, $"INCOME ${SectorSiteIncome(state, selectedSector)}", new Vector2(480, 78), Color.Lime, 1);
        font.Draw(batch, $"CHAOS {selectedSector.Chaos}", new Vector2(480, 92), Color.Lime, 1);
        var selectedLabel = selectedGang is null ? "NO GANG" : $"GANG {selectedGang.Id.Value}";
        font.Draw(batch, _message.Length <= 32 ? _message : _message[..32],
            new Vector2(438, 374), Color.Gold, 1);
        font.Draw(batch, selectedLabel, new Vector2(438, 393), PlayerColors[playerIndex], 1);
        DrawButton(batch, pixel, font, CityAction,
            state.Coordinator.Phase == TurnPhase.Hire ? "HIRE" : "ACTION", false);
        DrawButton(batch, pixel, font, CityAdvance, "ADVANCE", false);
        font.Draw(batch, "ARROWS ENTER/H/SPACE  F5/F9 SAVE  F6/F10 REPLAY", new Vector2(18, 439), new Color(180, 190, 190), 1);
    }

    private void DrawEndgame(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        if (_cityBackground is not null)
            batch.Draw(_cityBackground, new Rectangle(0, 0, 640, 460), Color.White);
        if (_endgameBackground is not null)
            batch.Draw(_endgameBackground, new Rectangle(0, 50, 428, 410), Color.White);
        else
            batch.Draw(pixel, new Rectangle(0, 50, 428, 410), new Color(0, 0, 0, 230));

        var outcome = state.Outcome!;
        font.Draw(batch, "MATCH COMPLETE", new Vector2(164, 62), Color.Gold, 2);
        font.Draw(batch, ScenarioCatalog.Get(outcome.Scenario).Name, new Vector2(164, 86), Color.White, 1);
        font.Draw(batch, $"TURN {outcome.Turn}  {outcome.Reason}", new Vector2(164, 101), Color.White, 1);

        var rows = outcome.Standings.Count > 0
            ? outcome.Standings.Select(standing => (
                standing.Player, Label: $"{standing.Place}. {state.FindPlayer(standing.Player)!.Setup.Name}",
                Value: standing.Score.ToString())).ToArray()
            : state.Players.OrderByDescending(player => outcome.Winners.Contains(player.Id))
                .ThenBy(player => player.Id.Value)
                .Select(player => (Player: player.Id,
                    Label: player.Setup.Name,
                    Value: outcome.Winners.Contains(player.Id) ? "WINNER" : ""))
                .ToArray();
        for (var index = 0; index < rows.Length; index++)
        {
            var y = 78 + index * 48;
            font.Draw(batch, rows[index].Label, new Vector2(8, y), PlayerColors[rows[index].Player.Value], 1);
            font.Draw(batch, rows[index].Value, new Vector2(164, y), Color.White, 1);
        }

        font.Draw(batch, "AWARDS", new Vector2(164, 255), Color.Gold, 1);
        for (var index = 0; index < outcome.Awards.Count; index++)
        {
            var award = outcome.Awards[index];
            var recipients = string.Join(",", award.Recipients.Select(player => (player.Value + 1).ToString()));
            font.Draw(batch, $"{award.Award} {award.Value} P{recipients}",
                new Vector2(164, 272 + index * 14), Color.White, 1);
        }
        DrawBorder(batch, pixel, EndgameDone, Color.Gold, 2);
    }

    private void DrawHandoff(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        batch.Draw(pixel, new Rectangle(0, 0, VirtualInput.Width, VirtualInput.Height), Color.Black);
        var panel = new Rectangle(266, 148, 108, 164);
        if (_handoffPanel is not null) batch.Draw(_handoffPanel, panel, Color.White);
        else batch.Draw(pixel, panel, new Color(24, 37, 39));
        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        var player = state.FindPlayer(playerId)!;
        DrawCentered(font, batch, player.Setup.Name, 194, PlayerColors[playerId.Value], 1);
        DrawBorder(batch, pixel, HandoffReady, Color.Gold, 2);
    }

    private void DrawEvents(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        if (_cityBackground is not null)
            batch.Draw(_cityBackground, new Rectangle(0, 0, 640, 460), Color.White);
        batch.Draw(pixel, new Rectangle(8, 48, 420, 402), new Color(0, 0, 0, 235));
        font.Draw(batch, "LAST TURN EVENTS", new Vector2(18, 60), Color.Gold, 2);
        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        font.Draw(batch, state.FindPlayer(playerId)!.Setup.Name, new Vector2(18, 84), PlayerColors[playerId.Value], 1);
        var notifications = state.NotificationsFor(playerId);
        if (notifications.Count == 0)
        {
            font.Draw(batch, "NO EVENTS", new Vector2(18, 112), Color.White, 1);
        }
        else
        {
            foreach (var entry in notifications.Take(18).Select((notification, index) => (notification, index)))
            {
                var notification = entry.notification;
                var detail = notification.Gang is { } gang ? $" GANG {gang.Value}" : "";
                if (notification.SectorId is { } sector) detail += $" SECTOR {sector + 1}";
                font.Draw(batch, $"T{notification.Turn} {notification.Kind}{detail}",
                    new Vector2(18, 110 + entry.index * 16), Color.White, 1);
            }
        }
        DrawButton(batch, pixel, font, EventsDismiss, "DISMISS", false);
        DrawButton(batch, pixel, font, EventsBack, "BACK", false);
    }

    private void DrawCommands(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        if (_cityBackground is not null)
            batch.Draw(_cityBackground, new Rectangle(0, 0, 640, 460), Color.White);
        batch.Draw(pixel, new Rectangle(8, 48, 420, 402), new Color(0, 0, 0, 235));
        font.Draw(batch, "COMMANDS", new Vector2(18, 60), Color.Gold, 2);
        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        var gang = SelectedGang(state.FindPlayer(playerId)!);
        var gangName = gang is null
            ? "NO ACTIVE GANG"
            : state.Definitions.Gangs.Single(definition => definition.Id == gang.DefinitionId).Name;
        font.Draw(batch, gangName, new Vector2(18, 84), PlayerColors[playerId.Value], 1);

        var first = Math.Max(0, _commandCursor - 8);
        foreach (var entry in _commandOptions.Skip(first).Take(18).Select((command, index) => (command, index)))
        {
            var optionIndex = first + entry.index;
            var y = 102 + entry.index * 16;
            if (optionIndex == _commandCursor)
                batch.Draw(pixel, new Rectangle(14, y - 3, 404, 14), new Color(72, 54, 18));
            font.Draw(batch, FormatCommand(state, entry.command), new Vector2(18, y), Color.White, 1);
        }
        if (_commandOptions.Count == 0)
            font.Draw(batch, "NO LEGAL COMMANDS", new Vector2(18, 102), Color.White, 1);
        DrawButton(batch, pixel, font, CommandsQueue, "QUEUE", false);
        DrawButton(batch, pixel, font, CommandsBack, "BACK", false);
    }

    private void DrawHire(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        if (_cityBackground is not null)
            batch.Draw(_cityBackground, new Rectangle(0, 0, 640, 460), Color.White);
        batch.Draw(pixel, new Rectangle(8, 48, 420, 402), new Color(0, 0, 0, 235));
        font.Draw(batch, "NEW RECRUITS", new Vector2(18, 60), Color.Gold, 2);
        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        var player = state.FindPlayer(playerId)!;
        font.Draw(batch, $"{player.Setup.Name}  CASH ${player.Cash}  SECTOR {_cursor + 1}",
            new Vector2(18, 84), PlayerColors[playerId.Value], 1);
        for (var index = 0; index < player.HirePool.Count; index++)
        {
            var y = 112 + index * 72;
            if (index == _hireCursor)
                batch.Draw(pixel, new Rectangle(14, y - 6, 404, 54), new Color(72, 54, 18));
            var definition = state.Definitions.Gangs.Single(gang => gang.Id == player.HirePool[index]);
            if (_gangPortraits is not null)
                batch.Draw(_gangPortraits, new Rectangle(20, y - 5, 48, 48),
                    OriginalSpriteLayout.GangPortrait(definition.Id), Color.White);
            font.Draw(batch, definition.Name, new Vector2(78, y), Color.White, 1);
            font.Draw(batch, $"FORCE {definition.Force}  UPKEEP {definition.Upkeep}  COST {HireRules.InitialCost(definition)}",
                new Vector2(78, y + 16), new Color(180, 230, 170), 1);
        }
        if (player.HirePool.Count == 0)
            font.Draw(batch, "NO HIRE OFFERS", new Vector2(18, 112), Color.White, 1);
        DrawButton(batch, pixel, font, HireQueue, "HIRE", false);
        DrawButton(batch, pixel, font, HireSnub, "SNUB", false);
        DrawButton(batch, pixel, font, HireBack, "BACK", false);
    }

    private void DrawSectorDetails(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        if (_cityBackground is not null)
            batch.Draw(_cityBackground, new Rectangle(0, 0, 640, 460), Color.White);
        batch.Draw(pixel, new Rectangle(8, 48, 420, 402), new Color(0, 0, 0, 235));
        var sector = state.Sectors[_cursor];
        font.Draw(batch, $"SECTOR {_cursor + 1}", new Vector2(18, 60), Color.Gold, 2);
        var owner = sector.Owner is { } ownerId ? state.FindPlayer(ownerId)!.Setup.Name : "NEUTRAL";
        font.Draw(batch, $"OWNER {owner}  INCOME {sector.Income}  TOLERANCE {sector.Tolerance}",
            new Vector2(18, 86), Color.White, 1);
        font.Draw(batch, $"CHAOS {sector.Chaos}  CRACKDOWN {(sector.CrackdownActive ? "YES" : "NO")}",
            new Vector2(18, 102), Color.White, 1);
        foreach (var site in sector.Sites)
        {
            var y = 136 + site.Slot * 76;
            var definition = state.Definitions.Sites.Single(value => value.Id == site.DefinitionId);
            if (_sitePortraits is not null)
                batch.Draw(_sitePortraits, new Rectangle(18, y - 5, 120, 64),
                    OriginalSpriteLayout.SitePortrait(definition.Id), Color.White);
            font.Draw(batch, definition.Name, new Vector2(146, y), new Color(180, 230, 170), 1);
            font.Draw(batch, $"RESISTANCE {site.Resistance}  CASH {definition.Cash}  SUPPORT {definition.Support}",
                new Vector2(146, y + 16), Color.White, 1);
            var influence = site.InfluencedBy is { } influencedBy
                ? state.FindPlayer(influencedBy)!.Setup.Name
                : "NONE";
            font.Draw(batch, "INFLUENCED BY " + influence, new Vector2(146, y + 32), Color.White, 1);
        }
        DrawButton(batch, pixel, font, ManagementBack, "BACK", false);
    }

    private void DrawGangDetails(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        if (_cityBackground is not null)
            batch.Draw(_cityBackground, new Rectangle(0, 0, 640, 460), Color.White);
        batch.Draw(pixel, new Rectangle(8, 48, 420, 402), new Color(0, 0, 0, 235));
        if (_gangInfoBackground is not null)
            batch.Draw(_gangInfoBackground, new Rectangle(42, 74, 344, 209), Color.White);
        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        var gang = SelectedGang(state.FindPlayer(playerId)!);
        if (gang is null)
        {
            font.Draw(batch, "NO ACTIVE GANG", new Vector2(54, 94), Color.White, 1);
        }
        else
        {
            var definition = state.Definitions.Gangs.Single(value => value.Id == gang.DefinitionId);
            var stats = EffectiveStatisticsCalculator.ForGang(state, gang);
            if (_gangPortraits is not null)
                batch.Draw(_gangPortraits, new Rectangle(54, 98, 64, 64),
                    OriginalSpriteLayout.GangPortrait(definition.Id), Color.White);
            batch.Draw(pixel, new Rectangle(138, 92, 238, 181), Color.Black);
            font.Draw(batch, definition.Name, new Vector2(144, 98), PlayerColors[playerId.Value], 1);
            font.Draw(batch, $"FORCE {gang.Force}  SECTOR {gang.SectorId + 1}  TECH {definition.TechLevel}",
                new Vector2(144, 114), Color.White, 1);
            string[] left =
            [
                $"COMBAT {stats.Combat}", $"DEFENSE {stats.Defense}", $"CHAOS {stats.Chaos}",
                $"CONTROL {stats.Control}", $"HEAL {stats.Heal}", $"INFLUENCE {stats.Influence}",
                $"RESEARCH {stats.Research}"
            ];
            string[] right =
            [
                $"STEALTH {stats.Stealth}", $"DETECT {stats.Detect}", $"STRENGTH {stats.Strength}",
                $"BLADE {stats.Blade}", $"RANGE {stats.Range}", $"FIGHTING {stats.Fighting}",
                $"M ARTS {stats.MartialArts}"
            ];
            for (var index = 0; index < left.Length; index++)
            {
                font.Draw(batch, left[index], new Vector2(144, 138 + index * 16), Color.White, 1);
                font.Draw(batch, right[index], new Vector2(252, 138 + index * 16), Color.White, 1);
            }
            var queued = gang.QueuedCommand?.Command.Action.ToString().ToUpperInvariant() ?? "NONE";
            font.Draw(batch, "COMMAND " + queued, new Vector2(18, 310), Color.Gold, 1);
            font.Draw(batch, "WEAPON " + ItemName(state, gang.WeaponItemId), new Vector2(18, 334), Color.White, 1);
            font.Draw(batch, "ARMOR " + ItemName(state, gang.ArmorItemId), new Vector2(18, 350), Color.White, 1);
            font.Draw(batch, "MISC " + ItemName(state, gang.MiscellaneousItemId), new Vector2(18, 366), Color.White, 1);
        }
        DrawButton(batch, pixel, font, ManagementBack, "BACK", false);
    }

    private void DrawFinance(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        DrawManagementPanel(batch, pixel);
        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        var player = state.FindPlayer(playerId)!;
        var forecast = EconomyResolver.Project(state, player);
        font.Draw(batch, "FINANCIAL", new Vector2(18, 60), Color.Gold, 2);
        font.Draw(batch, player.Setup.Name, new Vector2(18, 86), PlayerColors[playerId.Value], 1);
        string[] rows =
        [
            $"CURRENT CASH       ${forecast.CurrentCash}",
            $"SECTOR TAXES       +${forecast.SectorIncome}",
            $"SITE INCOME        +${forecast.SiteIncome}",
            $"GANG UPKEEP        -${forecast.GangUpkeep}",
            "---------------------------",
            $"NEXT BALANCE       ${forecast.ResultCash}",
            $"NET CHANGE         {Signed(forecast.NetChange)}",
            "",
            $"TOTAL CASH EARNED  ${player.Statistics.CashEarned}",
            $"TOTAL CASH SPENT   ${player.Statistics.CashSpent}"
        ];
        for (var index = 0; index < rows.Length; index++)
            font.Draw(batch, rows[index], new Vector2(18, 120 + index * 22), Color.White, 1);
        font.Draw(batch, "PROJECTED AT NEXT UPKEEP", new Vector2(18, 368), new Color(180, 230, 170), 1);
        DrawButton(batch, pixel, font, ManagementBack, "BACK", false);
    }

    private void DrawCombatSummary(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        DrawManagementPanel(batch, pixel);
        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        font.Draw(batch, "COMBAT SUMMARY", new Vector2(18, 60), Color.Gold, 2);
        font.Draw(batch, state.FindPlayer(playerId)!.Setup.Name, new Vector2(18, 86),
            PlayerColors[playerId.Value], 1);
        var events = state.Events
            .Where(gameEvent => IsVisibleCombatEvent(state, playerId, gameEvent))
            .OrderByDescending(gameEvent => gameEvent.Sequence)
            .Take(12)
            .Reverse()
            .ToArray();
        if (events.Length == 0)
        {
            font.Draw(batch, "NO COMBAT TO REPORT", new Vector2(18, 116), Color.White, 1);
        }
        for (var index = 0; index < events.Length; index++)
        {
            var gameEvent = events[index];
            var y = 112 + index * 24;
            font.Draw(batch, $"T{gameEvent.Turn} {CombatHeading(state, gameEvent)}",
                new Vector2(18, y), Color.White, 1);
            font.Draw(batch, CombatResult(gameEvent), new Vector2(230, y),
                gameEvent.Kind == GameEventKind.CommandFailed ? Color.OrangeRed : new Color(180, 230, 170), 1);
        }
        DrawButton(batch, pixel, font, ManagementBack, "BACK", false);
    }

    private void DrawSearch(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        DrawManagementPanel(batch, pixel);
        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        font.Draw(batch, $"SEARCH SECTOR {_cursor + 1}", new Vector2(18, 60), Color.Gold, 2);
        font.Draw(batch, state.FindPlayer(playerId)!.Setup.Name, new Vector2(18, 86),
            PlayerColors[playerId.Value], 1);
        var visible = state.Players
            .SelectMany(player => player.Gangs)
            .Where(gang => gang.IsActive && gang.SectorId == _cursor
                && (gang.Owner == playerId || state.CanPlayerDetectGang(playerId, gang.Id)))
            .OrderBy(gang => gang.Owner.Value)
            .ThenBy(gang => gang.Id.Value)
            .ToArray();
        if (visible.Length == 0)
            font.Draw(batch, "NO GANGS DETECTED", new Vector2(18, 116), Color.White, 1);
        foreach (var entry in visible.Take(14).Select((gang, index) => (gang, index)))
        {
            var gang = entry.gang;
            var definition = state.Definitions.Gangs.Single(value => value.Id == gang.DefinitionId);
            var owner = state.FindPlayer(gang.Owner)!;
            var y = 112 + entry.index * 21;
            font.Draw(batch, definition.Name, new Vector2(18, y), PlayerColors[gang.Owner.Value], 1);
            font.Draw(batch, $"{owner.Setup.Name}  FORCE {gang.Force}"
                + (gang.Hidden ? "  HIDDEN" : ""), new Vector2(190, y), Color.White, 1);
        }
        if (visible.Length > 14)
            font.Draw(batch, $"+{visible.Length - 14} MORE", new Vector2(18, 390), Color.White, 1);
        DrawButton(batch, pixel, font, ManagementBack, "BACK", false);
    }

    private void DrawRanking(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        DrawManagementPanel(batch, pixel);
        var scenario = ScenarioCatalog.Get(state.Setup.Scenario);
        font.Draw(batch, "RANKING", new Vector2(18, 60), Color.Gold, 2);
        font.Draw(batch, scenario.Name, new Vector2(18, 86), Color.White, 1);
        font.Draw(batch, scenario.Objective.ToUpperInvariant(), new Vector2(18, 104), new Color(180, 230, 170), 1);

        if (scenario.IsTimed)
        {
            var standings = EndgameRankingEvaluator.EvaluateTimed(state);
            foreach (var entry in standings.Select((standing, index) => (standing, index)))
            {
                var player = state.FindPlayer(entry.standing.Player)!;
                var y = 142 + entry.index * 38;
                font.Draw(batch, $"{entry.standing.Place}. {player.Setup.Name}", new Vector2(18, y),
                    PlayerColors[player.Id.Value], 1);
                font.Draw(batch, $"SCORE {entry.standing.Score}", new Vector2(234, y), Color.White, 1);
            }
            var turns = ScenarioCatalog.Turns(state.Setup.Duration);
            font.Draw(batch, $"TURN {state.Coordinator.Turn} OF {turns}", new Vector2(18, 382), Color.White, 1);
        }
        else
        {
            foreach (var entry in state.Players.OrderBy(player => player.Id.Value)
                         .Select((player, index) => (player, index)))
            {
                var score = MatchOutcomeEvaluator.Project(state, entry.player);
                var y = 142 + entry.index * 38;
                font.Draw(batch, entry.player.Setup.Name, new Vector2(18, y),
                    PlayerColors[entry.player.Id.Value], 1);
                font.Draw(batch, ObjectiveProgress(state.Setup.Scenario, score),
                    new Vector2(170, y), Color.White, 1);
            }
        }
        DrawButton(batch, pixel, font, ManagementBack, "BACK", false);
    }

    private void DrawItems(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        if (_cityBackground is not null)
            batch.Draw(_cityBackground, new Rectangle(0, 0, 640, 460), Color.White);
        batch.Draw(pixel, new Rectangle(8, 48, 624, 402), new Color(0, 0, 0, 240));
        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        var player = state.FindPlayer(playerId)!;
        var gang = SelectedGang(player);
        var items = RealItems(state);
        font.Draw(batch, "RESEARCH AND EQUIPMENT", new Vector2(18, 60), Color.Gold, 2);
        font.Draw(batch, gang is null ? "NO ACTIVE GANG" :
            $"{state.Definitions.Gangs.Single(value => value.Id == gang.DefinitionId).Name}  CASH ${player.Cash}",
            new Vector2(18, 86), PlayerColors[playerId.Value], 1);

        var first = Math.Max(0, _itemCursor - 8);
        foreach (var entry in items.Skip(first).Take(18).Select((item, index) => (item, index)))
        {
            var itemIndex = first + entry.index;
            var y = 108 + entry.index * 16;
            if (itemIndex == _itemCursor)
                batch.Draw(pixel, new Rectangle(14, y - 3, 316, 14), new Color(72, 54, 18));
            var marker = player.ResearchedItems.Contains(entry.item.Id) || entry.item.ResearchDifficulty == 0
                ? "+"
                : player.ResearchProgress.ContainsKey(entry.item.Id) ? ">" : " ";
            font.Draw(batch, $"{marker} {entry.item.Name}", new Vector2(18, y), Color.White, 1);
        }

        if (items.Length > 0)
        {
            var item = items[_itemCursor];
            var remaining = player.RemainingResearch(state.Definitions, item.Id);
            var researched = remaining == 0 ? "COMPLETE" : $"{remaining} REMAIN";
            font.Draw(batch, item.Name, new Vector2(350, 112), Color.Gold, 1);
            font.Draw(batch, $"{EquipmentRules.SlotFor(item).ToString().ToUpperInvariant()}  TECH {item.TechLevel}",
                new Vector2(350, 136), Color.White, 1);
            font.Draw(batch, $"COST ${item.Cost}", new Vector2(350, 152), Color.White, 1);
            font.Draw(batch, $"RESEARCH {researched}", new Vector2(350, 168), Color.White, 1);
            font.Draw(batch, "MODIFIERS", new Vector2(350, 202), new Color(180, 230, 170), 1);
            var modifiers = ItemModifiers(item).ToArray();
            for (var index = 0; index < modifiers.Length; index++)
                font.Draw(batch, modifiers[index], new Vector2(350, 220 + index * 16), Color.White, 1);
            if (gang is not null)
            {
                var equipped = EquipmentRules.EquippedItem(gang, EquipmentRules.SlotFor(item));
                font.Draw(batch, equipped == item.Id ? "EQUIPPED" : "NOT EQUIPPED",
                    new Vector2(350, 348), equipped == item.Id ? Color.Lime : Color.White, 1);
            }
        }
        font.Draw(batch, "UP/DOWN ITEM  LEFT/RIGHT GANG", new Vector2(18, 395), Color.White, 1);
        DrawButton(batch, pixel, font, ItemsResearch, "RESEARCH", false);
        DrawButton(batch, pixel, font, ItemsEquip, "EQUIP", false);
        DrawButton(batch, pixel, font, ItemsSell, "SELL", false);
        DrawButton(batch, pixel, font, ItemsBack, "BACK", false);
    }

    private void DrawManagementPanel(SpriteBatch batch, Texture2D pixel)
    {
        if (_cityBackground is not null)
            batch.Draw(_cityBackground, new Rectangle(0, 0, 640, 460), Color.White);
        batch.Draw(pixel, new Rectangle(8, 48, 420, 402), new Color(0, 0, 0, 235));
    }

    private static string Signed(int value) => value >= 0 ? $"+${value}" : $"-${Math.Abs(value)}";

    private static Rechaos.Core.Assets.ItemDefinition[] RealItems(MatchState state) =>
        state.Definitions.Items.Where(item => item.Type != 99).OrderBy(item => item.Id).ToArray();

    private static IEnumerable<string> ItemModifiers(Rechaos.Core.Assets.ItemDefinition item)
    {
        var stats = item.Stats;
        (string Name, int Value)[] values =
        [
            ("COMBAT", stats.Combat), ("DEFENSE", stats.Defense), ("STEALTH", stats.Stealth),
            ("DETECT", stats.Detect), ("CHAOS", stats.Chaos), ("CONTROL", stats.Control),
            ("HEAL", stats.Heal), ("INFLUENCE", stats.Influence), ("RESEARCH", stats.Research),
            ("STRENGTH", stats.Strength), ("BLADE", stats.Blade), ("RANGE", stats.Range),
            ("FIGHTING", stats.Fighting), ("M ARTS", stats.MartialArts)
        ];
        var any = false;
        foreach (var value in values.Where(value => value.Value != 0).Take(8))
        {
            any = true;
            yield return $"{value.Name} {(value.Value > 0 ? "+" : "")}{value.Value}";
        }
        if (!any) yield return "NONE";
    }

    private static string ObjectiveProgress(ScenarioId scenario, PlayerScoreState score) => scenario switch
    {
        ScenarioId.KillEmAll => score.IsAlive ? $"ALIVE  FOES {score.OpponentsAlive}" : "ELIMINATED",
        ScenarioId.Big40 => $"SECTORS {score.ControlledSectors}/40",
        ScenarioId.Eliminate => $"ENEMY RIGHT HANDS {score.OpposingRightHandsAlive}",
        ScenarioId.Siege => $"IMPORTANT {score.ImportantSectorsControlled}/6",
        ScenarioId.BigMan => $"POINTS {score.BigManPoints}/40",
        ScenarioId.Armageddon => $"SECTORS {score.ControlledSectors}/{MatchLimits.SectorCount}",
        _ => ""
    };

    private static bool IsVisibleCombatEvent(MatchState state, PlayerId viewer, GameEvent gameEvent)
    {
        if (gameEvent.Kind == GameEventKind.PoliceAttackResolved) return gameEvent.Player == viewer;
        if (gameEvent.Action != GangAction.Attack || gameEvent.Resolution is null) return false;
        if (gameEvent.Player == viewer) return true;
        return gameEvent.Target.Kind == CommandTargetKind.Gang
            && state.FindGang(new GangId(gameEvent.Target.Id))?.Owner == viewer;
    }

    private static string CombatHeading(MatchState state, GameEvent gameEvent)
    {
        if (gameEvent.Kind == GameEventKind.PoliceAttackResolved)
            return "POLICE > " + GangLabel(state, gameEvent.Gang);
        return GangLabel(state, gameEvent.Gang) + " > "
            + GangLabel(state, new GangId(gameEvent.Target.Id));
    }

    private static string CombatResult(GameEvent gameEvent)
    {
        if (gameEvent.PoliceAttack is { } police)
            return police.Detected ? $"DAMAGE {police.Damage}" : "NOT DETECTED";
        var resolution = gameEvent.Resolution!;
        return resolution.Code == CommandResolutionCode.TargetEvaded
            ? "TARGET EVADED"
            : $"DAMAGE {resolution.Damage} RETURN {resolution.RetaliationDamage}";
    }

    private static string GangLabel(MatchState state, GangId? gangId)
    {
        if (gangId is not { } id) return "GANG";
        var gang = state.FindGang(id);
        return gang is null ? $"GANG {id.Value}" :
            state.Definitions.Gangs.Single(value => value.Id == gang.DefinitionId).Name;
    }

    private static string ItemName(MatchState state, short? itemId) =>
        itemId is { } id ? state.Definitions.Items[id].Name : "NONE";

    private static string FormatCommand(MatchState state, GameCommand command)
    {
        var text = command.Action.ToString().ToUpperInvariant();
        if (command.Target.Kind != CommandTargetKind.None)
            text += " " + FormatTarget(state, command.Target);
        if (command.SecondaryTarget is { } secondary)
            text += " / " + FormatTarget(state, secondary);
        return text;
    }

    private static string FormatTarget(MatchState state, CommandTarget target) => target.Kind switch
    {
        CommandTargetKind.Gang => "GANG " + target.Id,
        CommandTargetKind.Sector => "SECTOR " + (target.Id + 1),
        CommandTargetKind.Site => state.Definitions.Sites.Single(definition => definition.Id ==
            state.FindSite(target.Id)!.DefinitionId).Name,
        CommandTargetKind.Item => state.Definitions.Items[target.Id].Name,
        _ => ""
    };

    private void DismissNotification()
    {
        if (_state is null || _replay is null) return;
        var playerId = _state.Coordinator.ActivePlayer ?? new PlayerId(0);
        _message = _replay.TryDismissNotification(playerId, out _)
            ? "EVENT DISMISSED"
            : "NO EVENT TO DISMISS";
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
        var gang = SelectedGang(_state.FindPlayer(playerId)!);
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

    private void OpenHire()
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
        _hireCursor = 0;
        _screens.Show(ClientScreen.Hire);
    }

    private void MoveHireCursor(int delta)
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId) return;
        var count = _state.FindPlayer(playerId)!.HirePool.Count;
        if (count > 0) _hireCursor = Mod(_hireCursor + delta, count);
    }

    private void HandleHireClick(Point point)
    {
        if (_state?.Coordinator.ActivePlayer is { } playerId
            && point.X is >= 14 and < 418 && point.Y is >= 106 and < 322)
        {
            var index = (point.Y - 106) / 72;
            if (index < _state.FindPlayer(playerId)!.HirePool.Count) _hireCursor = index;
        }
        else if (HireQueue.Contains(point)) QueueSelectedHireOffer();
        else if (HireSnub.Contains(point)) SnubSelectedHireOffer();
        else if (HireBack.Contains(point)) _screens.Show(ClientScreen.City);
    }

    private void QueueSelectedHireOffer()
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId || _replay is null) return;
        var player = _state.FindPlayer(playerId)!;
        if (player.HirePool.Count == 0) return;
        _hireCursor = Math.Clamp(_hireCursor, 0, player.HirePool.Count - 1);
        var offer = player.HirePool[_hireCursor];
        var result = _replay!.QueueHire(playerId, offer, _cursor);
        _message = result.Accepted ? "HIRE QUEUED" : result.Validation.Message.ToUpperInvariant();
        if (result.Accepted) _screens.Show(ClientScreen.City);
    }

    private void SnubSelectedHireOffer()
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId || _replay is null) return;
        var player = _state.FindPlayer(playerId)!;
        if (player.HirePool.Count == 0) return;
        _hireCursor = Math.Clamp(_hireCursor, 0, player.HirePool.Count - 1);
        var offer = player.HirePool[_hireCursor];
        var result = _replay.SnubHireOffer(playerId, offer);
        _message = result.Accepted ? "OFFER SNUBBED" : result.Validation.Message.ToUpperInvariant();
        _hireCursor = Math.Clamp(_hireCursor, 0, Math.Max(0, player.HirePool.Count - 1));
    }

    private void AdvancePhase()
    {
        if (_state is null) return;
        if (_state.Outcome is not null)
        {
            _message = "MATCH COMPLETE";
            return;
        }
        var previousActivePlayer = _state.Coordinator.ActivePlayer;
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
        if (_state.Outcome is not null)
            _screens.Show(ClientScreen.Endgame);
        else if (transition.ActivePlayer is not null && transition.ActivePlayer != previousActivePlayer)
        {
            _selectedGangIndex = 0;
            _screens.Show(ClientScreen.Handoff);
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
            _selectedGangIndex = 0;
            _message = result.RecoveredFromBackup ? "BACKUP GAME LOADED" : "GAME LOADED";
            _screens.Show(_state.Outcome is null ? ClientScreen.City : ClientScreen.Endgame);
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

    private Texture2D? LoadTexture(string fileName)
    {
        var path = Path.Combine(_assetRoot, "images", fileName);
        if (!File.Exists(path)) return null;
        using var stream = File.OpenRead(path);
        return Texture2D.FromStream(GraphicsDevice, stream);
    }

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

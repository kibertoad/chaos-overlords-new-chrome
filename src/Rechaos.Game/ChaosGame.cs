using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;

namespace Rechaos.Game;

public sealed partial class ChaosGame : Microsoft.Xna.Framework.Game
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
    private static readonly Rectangle[] SetupAiMentalities =
    [
        new(80, 330, 108, 27), new(80, 359, 108, 27),
        new(80, 388, 108, 27), new(80, 417, 108, 27)
    ];
    private static readonly Rectangle SetupStart = new(370, 374, 92, 50);
    private static readonly Rectangle SetupBack = new(466, 374, 96, 50);
    private static readonly Rectangle CityDone = new(492, 278, 106, 54);
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
    private static readonly Rectangle ManagementBack = new(322, 414, 96, 28);
    private static readonly Rectangle ItemsResearch = new(10, 414, 96, 28);
    private static readonly Rectangle ItemsEquip = new(114, 414, 96, 28);
    private static readonly Rectangle ItemsGive = new(218, 414, 96, 28);
    private static readonly Rectangle ItemsSell = new(322, 414, 96, 28);
    private static readonly Rectangle ItemsBack = new(426, 414, 96, 28);
    private static readonly Rectangle GiveQueue = new(218, 414, 96, 28);
    private static readonly Rectangle GiveBack = new(322, 414, 96, 28);
    private static readonly Rectangle EndgameDone = new(320, 404, 104, 54);
    private static readonly Rectangle HandoffReady = new(266, 246, 108, 66);
    private readonly GraphicsDeviceManager _graphics;
    private readonly string _assetRoot;
    private readonly string _quickSavePath;
    private readonly string _autoSavePath;
    private readonly string _replayPath;
    private readonly bool _debugPhaseStepping;
    private SpriteBatch? _batch;
    private Texture2D? _pixel;
    private Texture2D? _titleBackground;
    private Texture2D? _setupBackground;
    private Texture2D? _cityBackground;
    private readonly Texture2D?[] _cityOwnershipLayers = new Texture2D?[MatchLimits.PlayerCount + 1];
    private Texture2D? _endgameBackground;
    private Texture2D? _handoffPanel;
    private Texture2D? _gangInfoBackground;
    private Texture2D? _siteInfoBackground;
    private Texture2D? _itemInfoBackground;
    private Texture2D? _combatBackground;
    private Texture2D? _combatResultsBackground;
    private Texture2D? _lastTurnEventsBackground;
    private readonly Texture2D?[] _lastTurnEventArtwork = new Texture2D?[10];
    private Texture2D? _hireComparisonBackground;
    private Texture2D? _influenceBackground;
    private Texture2D? _targetAcquisitionBackground;
    private Texture2D? _equipmentPurchaseBackground;
    private Texture2D? _equipmentResearchBackground;
    private Texture2D? _sitePortraits;
    private Texture2D? _gangPortraits;
    private Texture2D? _itemPortraits;
    private Texture2D? _policeSprites;
    private Texture2D? _uiSprites;
    private PixelFont? _font;
    private readonly Dictionary<short, SoundEffect> _weaponSounds = [];
    private readonly Dictionary<string, Texture2D> _combatAnimationTextures = [];
    private readonly CombatAnimationPlayer _combatAnimationPlayer = new();
    private MatchState? _state;
    private MatchReplayRecorder? _replay;
    private OriginalData? _definitions;
    private readonly ScreenRouter _screens = new();
    private readonly CitySectorClickTracker _citySectorClicks = new();
    private readonly IndexedDoubleClickTracker _sectorGangClicks = new();
    private readonly IndexedDoubleClickTracker _sectorSiteClicks = new();
    private readonly IndexedDoubleClickTracker _influenceSiteClicks = new();
    private readonly IndexedDoubleClickTracker _equipmentItemClicks = new();
    private readonly IndexedDoubleClickTracker _hirePortraitClicks = new();
    private readonly bool[] _computerPlayers = new bool[MatchLimits.PlayerCount];
    private readonly short[] _playerPortraits = Enumerable.Range(0, MatchLimits.PlayerCount)
        .Select(index => checked((short)index)).ToArray();
    private AiDifficulty _selectedAiMentality = AiDifficulty.Criminal;
    private ScenarioId _selectedScenario = ScenarioId.Greed;
    private GameDuration _selectedDuration = GameDuration.SixMonths;
    private int _selectedPlayerCount = 2;
    private int _cursor;
    private int _selectedGangIndex;
    private IReadOnlyList<GameCommand> _commandOptions = [];
    private IReadOnlyList<GameCommand> _commandTargetOptions = [];
    private int _commandCursor;
    private int _commandTargetCursor;
    private int _equipmentCategory;
    private bool _choosingCommandTarget;
    private bool _commandRepeats;
    private ClientScreen _commandReturnScreen = ClientScreen.City;
    private ClientScreen _managementReturnScreen = ClientScreen.City;
    private int _hireCursor;
    private int _itemCursor;
    private int _combatSummaryCursor;
    private int _eventCursor;
    private IReadOnlyList<GameCommand> _giveOptions = [];
    private int _giveCursor;
    private int? _draggedHireSlot;
    private short? _draggedHireDefinitionId;
    private Point _hirePressPoint;
    private bool _hireDragStarted;
    private GangId? _draggedGangId;
    private Point _gangPressPoint;
    private bool _gangDragStarted;
    private int? _pendingHireSlot;
    private Point _dragPoint;
    private Point? _hoverPoint;
    private string _message = "SELECT NEW GAME";
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;
    private long _lastAudibleEventSequence = -1;
    private long _lastAnimatedEventSequence = -1;
    private TimeSpan _inputTime;
    private ClientScreen _gangDetailsReturnScreen = ClientScreen.City;
    private GangId? _gangDetailsInstanceId;
    private short? _gangDetailsDefinitionId;
    private ClientScreen _siteDetailsReturnScreen = ClientScreen.Sector;
    private int? _siteDetailsSectorId;
    private int? _siteDetailsSlot;
    private short? _itemDetailsId;

    public ChaosGame(string assetRoot, bool debugPhaseStepping = false)
    {
        _assetRoot = assetRoot;
        _debugPhaseStepping = debugPhaseStepping;
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
        _computerPlayers[1] = true;
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
        _siteInfoBackground = LoadTexture("PX05002.bmp");
        _itemInfoBackground = LoadTexture("PX05001.bmp");
        _combatBackground = LoadTexture("PX05014.bmp");
        _combatResultsBackground = LoadTexture("PX05012.bmp");
        _lastTurnEventsBackground = LoadTexture("PX05010.bmp");
        for (var eventArt = 1; eventArt <= 9; eventArt++)
            _lastTurnEventArtwork[eventArt] = LoadTexture($"PX060{eventArt:00}.bmp");
        _hireComparisonBackground = LoadTexture("PX05016.bmp");
        _influenceBackground = LoadTexture("PX05005.bmp");
        _targetAcquisitionBackground = LoadTexture("PX05003.bmp");
        _equipmentPurchaseBackground = LoadTexture("PX05004.bmp");
        _equipmentResearchBackground = LoadTexture("PX05007.bmp");
        _sitePortraits = LoadTexture("PX02000.bmp");
        _gangPortraits = LoadTexture("PX03000.bmp");
        _itemPortraits = LoadTexture("PX04999.bmp", transparentBlack: true);
        _policeSprites = LoadTexture("PX00300.bmp", transparentBlack: true);
        _uiSprites = LoadTexture("PX00129.bmp", transparentWhite: true);
        LoadCombatAnimationTextures();
        for (short index = 0; index <= 18; index++)
        {
            var sound = LoadSound(AudioRouting.SoundFile(index));
            if (sound is not null) _weaponSounds.Add(index, sound);
        }
    }

    protected override void Update(GameTime gameTime)
    {
        _inputTime = gameTime.TotalGameTime;
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();
        RunComputerTurns();
        CaptureNewCombatAnimations();
        _combatAnimationPlayer.Advance(gameTime.ElapsedGameTime);
        if (_combatAnimationPlayer.IsPlaying)
        {
            PlayNewCombatSounds();
            _previousKeyboard = keyboard;
            _previousMouse = mouse;
            base.Update(gameTime);
            return;
        }
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
                    FinishHandoff();
                break;
            case ClientScreen.Events:
                if (Pressed(keyboard, Keys.Left) || Pressed(keyboard, Keys.Up)) MoveEventCursor(-1);
                if (Pressed(keyboard, Keys.Right) || Pressed(keyboard, Keys.Down)) MoveEventCursor(1);
                if (Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Delete)) CloseEvents();
                if (Pressed(keyboard, Keys.Back)) CloseEvents();
                break;
            case ClientScreen.Commands:
                if (Pressed(keyboard, Keys.Up)) MoveCommandCursor(-1);
                if (Pressed(keyboard, Keys.Down)) MoveCommandCursor(1);
                if (Pressed(keyboard, Keys.Enter)) ActivateCommandSelection();
                if (Pressed(keyboard, Keys.Back)) BackFromCommands();
                break;
            case ClientScreen.Hire:
                if (Pressed(keyboard, Keys.Left) || Pressed(keyboard, Keys.Up)) MoveHireCursor(-1);
                if (Pressed(keyboard, Keys.Right) || Pressed(keyboard, Keys.Down)) MoveHireCursor(1);
                if (Pressed(keyboard, Keys.S)) SnubSelectedHireOffer();
                if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
                    _screens.Show(_managementReturnScreen);
                break;
            case ClientScreen.Sector:
                UpdateSector(keyboard);
                break;
            case ClientScreen.Gang:
                if (_gangDetailsInstanceId is not null)
                {
                    if (Pressed(keyboard, Keys.Left) || Pressed(keyboard, Keys.Up)) CycleGang(-1);
                    if (Pressed(keyboard, Keys.Right) || Pressed(keyboard, Keys.Down)) CycleGang(1);
                }
                if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
                    CloseGangDetails();
                break;
            case ClientScreen.Site:
                if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
                    CloseSiteDetails();
                break;
            case ClientScreen.ItemInformation:
                if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
                    CloseItemDetails();
                break;
            case ClientScreen.Finance:
            case ClientScreen.Ranking:
                if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
                    _screens.Show(_managementReturnScreen);
                break;
            case ClientScreen.Items:
                if (Pressed(keyboard, Keys.Up)) MoveItemCursor(-1);
                if (Pressed(keyboard, Keys.Down)) MoveItemCursor(1);
                if (Pressed(keyboard, Keys.Left) || Pressed(keyboard, Keys.G)) CycleGang(-1);
                if (Pressed(keyboard, Keys.Right)) CycleGang(1);
                if (Pressed(keyboard, Keys.R)) QueueItemCommand(GangAction.Research);
                if (Pressed(keyboard, Keys.E)) QueueItemCommand(GangAction.Equip);
                if (Pressed(keyboard, Keys.V)) OpenGiveTargets();
                if (Pressed(keyboard, Keys.S)) QueueItemCommand(GangAction.Sell);
                if (Pressed(keyboard, Keys.Back)) _screens.Show(ClientScreen.City);
                break;
            case ClientScreen.Give:
                if (Pressed(keyboard, Keys.Up)) MoveGiveCursor(-1);
                if (Pressed(keyboard, Keys.Down)) MoveGiveCursor(1);
                if (Pressed(keyboard, Keys.Enter)) QueueSelectedGive();
                if (Pressed(keyboard, Keys.Back)) _screens.Show(ClientScreen.Items);
                break;
            case ClientScreen.CombatSummary:
                if (Pressed(keyboard, Keys.Left) || Pressed(keyboard, Keys.Up)) MoveCombatSummary(-1);
                if (Pressed(keyboard, Keys.Right) || Pressed(keyboard, Keys.Down)) MoveCombatSummary(1);
                if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
                    _screens.Show(_managementReturnScreen);
                break;
            case ClientScreen.Search:
                if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
                    _screens.Show(_managementReturnScreen);
                break;
        }
        var pointerMapped = VirtualInput.TryMap(GraphicsDevice.Viewport, mouse.Position, out var virtualPoint);
        _hoverPoint = pointerMapped ? virtualPoint : null;
        if (pointerMapped && mouse.LeftButton == ButtonState.Pressed)
        {
            _dragPoint = virtualPoint;
            if (_previousMouse.LeftButton == ButtonState.Released) HandleClick(virtualPoint);
            else if (_draggedHireDefinitionId is not null && !_hireDragStarted
                     && (Math.Abs(virtualPoint.X - _hirePressPoint.X) >= 4
                         || Math.Abs(virtualPoint.Y - _hirePressPoint.Y) >= 4))
            {
                _hireDragStarted = true;
                _message = "DROP ON A CONTROLLED SECTOR";
            }
            else if (_draggedGangId is not null && !_gangDragStarted
                     && (Math.Abs(virtualPoint.X - _gangPressPoint.X) >= 4
                         || Math.Abs(virtualPoint.Y - _gangPressPoint.Y) >= 4))
            {
                _gangDragStarted = true;
                _message = "DROP ON A NEIGHBORING SECTOR";
            }
        }
        if (_previousMouse.LeftButton == ButtonState.Pressed && mouse.LeftButton == ButtonState.Released
            && _draggedHireDefinitionId is not null)
        {
            if (pointerMapped && _hireDragStarted) CompleteHireDrag(virtualPoint);
            else if (pointerMapped) CompleteHireClick();
            else CancelHireDrag();
        }
        else if (_previousMouse.LeftButton == ButtonState.Pressed && mouse.LeftButton == ButtonState.Released
                 && _draggedGangId is not null)
        {
            if (pointerMapped && _gangDragStarted) CompleteGangDrag(virtualPoint);
            else if (pointerMapped) CompleteGangClick();
            else CancelGangDrag();
        }
        PlayNewCombatSounds();
        CaptureNewCombatAnimations();
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
            case ClientScreen.Site when _state is not null:
                DrawSiteDetails(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.ItemInformation when _state is not null:
                DrawItemDetails(_batch, _pixel, _font, _state);
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
            case ClientScreen.Give when _state is not null:
                DrawGiveTargets(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.CombatSummary when _state is not null:
                DrawCombatSummary(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Search when _state is not null:
                DrawSearch(_batch, _pixel, _font, _state);
                break;
        }
        if (_state is not null && _combatAnimationPlayer.IsPlaying)
            DrawCombatAnimation(_batch, _pixel, _font, _state);
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
        Keys[] controllerKeys = [Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6];
        for (var index = 0; index < _selectedPlayerCount; index++)
            if (Pressed(keyboard, controllerKeys[index])) ToggleController(index);
        if (Pressed(keyboard, Keys.M)) CycleDifficulty();
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
        if (Pressed(keyboard, Keys.Space)) AdvanceTurn();
        if (Pressed(keyboard, Keys.F5)) SaveQuickGame();
        if (Pressed(keyboard, Keys.F9)) LoadQuickGame();
        if (Pressed(keyboard, Keys.F6)) SaveReplay();
        if (Pressed(keyboard, Keys.F10)) LoadReplay();
    }

    private void UpdateSector(KeyboardState keyboard)
    {
        var column = _cursor % 8;
        var row = _cursor / 8;
        if (Pressed(keyboard, Keys.Left) && column > 0) _cursor--;
        if (Pressed(keyboard, Keys.Right) && column < 7) _cursor++;
        if (Pressed(keyboard, Keys.Up) && row > 0) _cursor -= 8;
        if (Pressed(keyboard, Keys.Down) && row < 7) _cursor += 8;
        if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
            _screens.Show(ClientScreen.City);
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
                var playerSlot = Enumerable.Range(0, _selectedPlayerCount)
                    .FirstOrDefault(index => SetupPlayerSlot(index).Contains(point), -1);
                var previousPortrait = Enumerable.Range(0, _selectedPlayerCount)
                    .FirstOrDefault(index => PlayerPortraitLayout.Previous(index).Contains(point), -1);
                var nextPortrait = Enumerable.Range(0, _selectedPlayerCount)
                    .FirstOrDefault(index => PlayerPortraitLayout.Next(index).Contains(point), -1);
                if (scenario >= 0) _selectedScenario = (ScenarioId)scenario;
                else if (duration >= 0) _selectedDuration = Durations[duration];
                else if (previousPortrait >= 0) CyclePortrait(previousPortrait, -1);
                else if (nextPortrait >= 0) CyclePortrait(nextPortrait, 1);
                else
                {
                    var mentality = Array.FindIndex(SetupAiMentalities, rectangle => rectangle.Contains(point));
                    if (mentality >= 0) SelectDifficulty((AiDifficulty)mentality);
                    else if (playerSlot >= 0) ToggleController(playerSlot);
                    else if (SetupPlayersAdd.Contains(point)) ChangePlayerCount(1);
                    else if (SetupPlayersRemove.Contains(point)) ChangePlayerCount(-1);
                    else if (SetupStart.Contains(point)) StartMatch();
                    else if (SetupBack.Contains(point)) _screens.Show(ClientScreen.Title);
                }
                break;
            case ClientScreen.City:
                HandleCityClick(point);
                break;
            case ClientScreen.Endgame:
                if (EndgameDone.Contains(point)) _screens.Show(ClientScreen.Title);
                break;
            case ClientScreen.Handoff:
                if (HandoffReady.Contains(point)) FinishHandoff();
                break;
            case ClientScreen.Events:
                HandleEventsClick(point);
                break;
            case ClientScreen.Commands:
                HandleCommandsClick(point);
                break;
            case ClientScreen.Hire:
                HandleHireClick(point);
                break;
            case ClientScreen.Sector:
                HandleSectorClick(point);
                break;
            case ClientScreen.Gang:
                if (EquipmentCommandLayout.Ok.Contains(point)) CloseGangDetails();
                break;
            case ClientScreen.Site:
                if (SiteInformationLayout.Ok.Contains(point)) CloseSiteDetails();
                break;
            case ClientScreen.ItemInformation:
                if (ItemInformationLayout.Ok.Contains(point)) CloseItemDetails();
                break;
            case ClientScreen.Finance:
            case ClientScreen.Ranking:
            case ClientScreen.Search:
                if (ManagementBack.Contains(point)) _screens.Show(_managementReturnScreen);
                break;
            case ClientScreen.CombatSummary:
                HandleCombatSummaryClick(point);
                break;
            case ClientScreen.Items:
                HandleItemsClick(point);
                break;
            case ClientScreen.Give:
                HandleGiveClick(point);
                break;
        }
    }

    private void HandleSectorClick(Point point)
    {
        if (SectorDetailLayout.Back.Contains(point) || ManagementBack.Contains(point))
        {
            _screens.Show(ClientScreen.City);
            return;
        }
        if (_state is null) return;
        if (HandleCityConsoleClick(point, ClientScreen.Sector)) return;
        var rejectSlot = Enumerable.Range(0, HireDockLayout.SlotCount)
            .FirstOrDefault(slot => HireDockLayout.Reject(slot).Contains(point), -1);
        var hireSlot = Enumerable.Range(0, HireDockLayout.SlotCount)
            .FirstOrDefault(slot => HireDockLayout.Portrait(slot).Contains(point), -1);
        if (rejectSlot >= 0)
        {
            SnubHireDockOffer(rejectSlot);
            return;
        }
        if (hireSlot >= 0)
        {
            BeginHireDrag(hireSlot, point);
            return;
        }
        if (SectorDetailLayout.TrySectorAt(point, _cursor, out var selectedSector))
        {
            _cursor = selectedSector;
            _message = $"SECTOR {_cursor + 1}";
            return;
        }
        var siteSlot = Enumerable.Range(0, MatchLimits.SitesPerSector)
            .FirstOrDefault(slot => SectorDetailLayout.SitePortrait(slot).Contains(point), -1);
        if (siteSlot >= 0)
        {
            if (_sectorSiteClicks.Register(_cursor * MatchLimits.SitesPerSector + siteSlot, _inputTime))
                OpenSiteDetails(_cursor, siteSlot, ClientScreen.Sector);
            return;
        }
        var playerId = _state.Coordinator.ActivePlayer ?? new PlayerId(0);
        var visible = SectorGangView.Visible(_state, playerId, _cursor)
            .OrderBy(gang => gang.Owner == playerId ? 0 : 1)
            .ThenBy(gang => gang.Id.Value)
            .Take(SectorGangCardLayout.VisibleCards).ToArray();
        var index = Enumerable.Range(0, visible.Length)
            .FirstOrDefault(value => SectorGangCardLayout.Frame(value).Contains(point), -1);
        if (index < 0) return;
        var gang = visible[index];
        if (gang.Owner != playerId)
        {
            _message = "ENEMY GANG DETECTED";
            return;
        }
        var ownGangs = _state.FindPlayer(playerId)!.Gangs.Where(candidate => candidate.IsActive).ToArray();
        _selectedGangIndex = Array.FindIndex(ownGangs, candidate => candidate.Id == gang.Id);
        if (gang.QueuedCommand is { } queued
            && SectorGangCardLayout.AssignedCommand(index).Contains(point))
            OpenCommands(queued.Command.Repeat, ClientScreen.Sector);
        else if (SectorGangCardLayout.OneOffAction(index).Contains(point))
            OpenCommands(repeat: false, returnScreen: ClientScreen.Sector);
        else if (SectorGangCardLayout.RepeatingAction(index).Contains(point))
            OpenCommands(repeat: true, returnScreen: ClientScreen.Sector);
        else if (SectorGangCardLayout.Portrait(index).Contains(point))
            BeginGangDrag(gang, point);
        else if (_sectorGangClicks.Register(gang.Id.Value, _inputTime))
            OpenGangDetails(gang, ClientScreen.Sector);
    }

    private void HandleCityClick(Point point)
    {
        var rejectSlot = Enumerable.Range(0, HireDockLayout.SlotCount)
            .FirstOrDefault(slot => HireDockLayout.Reject(slot).Contains(point), -1);
        var hireSlot = Enumerable.Range(0, HireDockLayout.SlotCount)
            .FirstOrDefault(slot => HireDockLayout.Portrait(slot).Contains(point), -1);
        if (rejectSlot >= 0)
        {
            SnubHireDockOffer(rejectSlot);
        }
        else if (hireSlot >= 0)
        {
            BeginHireDrag(hireSlot, point);
        }
        else if (CityMapLayout.TrySectorAt(point, out var selected))
        {
            _cursor = selected;
            _message = $"SECTOR {_cursor + 1}";
            if (_citySectorClicks.Register(selected, _inputTime))
            {
                _screens.Show(ClientScreen.Sector);
                _message = $"SECTOR {_cursor + 1} DETAIL";
            }
        }
        else
        {
            _citySectorClicks.Cancel();
            HandleCityConsoleClick(point, ClientScreen.City);
        }
    }

    private bool HandleCityConsoleClick(Point point, ClientScreen returnScreen)
    {
        if (CityDone.Contains(point)) AdvanceTurn();
        else if (CityEvents.Contains(point)) OpenManagement(ClientScreen.Events, returnScreen);
        else if (CityCombatSummary.Contains(point)) OpenManagement(ClientScreen.CombatSummary, returnScreen);
        else if (CityFinance.Contains(point)) OpenManagement(ClientScreen.Finance, returnScreen);
        else if (CityGangs.Contains(point)) OpenSelectedGangDetails(returnScreen);
        else if (CityHire.Contains(point)) OpenHire(returnScreen);
        else if (CitySector.Contains(point)) _screens.Show(ClientScreen.Sector);
        else if (CityRanking.Contains(point)) OpenManagement(ClientScreen.Ranking, returnScreen);
        else if (CitySearch.Contains(point)) OpenManagement(ClientScreen.Search, returnScreen);
        else return false;
        return true;
    }

    private void OpenManagement(ClientScreen screen, ClientScreen returnScreen)
    {
        _managementReturnScreen = returnScreen;
        if (screen == ClientScreen.CombatSummary) _combatSummaryCursor = 0;
        if (screen == ClientScreen.Events) _eventCursor = 0;
        _screens.Show(screen);
    }

    private void OpenCommands(bool repeat = false, ClientScreen returnScreen = ClientScreen.City)
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
        _commandTargetOptions = [];
        _commandTargetCursor = 0;
        _choosingCommandTarget = false;
        _commandRepeats = repeat;
        _commandReturnScreen = returnScreen;
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
        else if (ItemsGive.Contains(point)) OpenGiveTargets();
        else if (ItemsSell.Contains(point)) QueueItemCommand(GangAction.Sell);
        else if (ItemsBack.Contains(point)) _screens.Show(ClientScreen.City);
    }

    private void OpenGiveTargets()
    {
        if (_state is null || _state.Coordinator.Phase != TurnPhase.Command
            || _state.Coordinator.ActivePlayer is not { } playerId)
        {
            _message = "GIVE REQUIRES THE COMMAND PHASE";
            return;
        }
        var player = _state.FindPlayer(playerId)!;
        var gang = SelectedGang(player);
        var items = RealItems(_state);
        if (gang is null || items.Length == 0)
        {
            _message = "NO ACTIVE GANG OR ITEM";
            return;
        }

        var itemId = items[_itemCursor].Id;
        _giveOptions = CommandOptionCatalog.LegalCommands(_state, playerId, gang.Id)
            .Where(command => command.Action == GangAction.Give
                && command.SecondaryTarget == CommandTarget.Item(itemId))
            .OrderBy(command => command.Target.Id)
            .ToArray();
        _giveCursor = 0;
        if (_giveOptions.Count == 0)
        {
            _message = "NO LEGAL RECIPIENT FOR EQUIPPED ITEM";
            return;
        }
        _screens.Show(ClientScreen.Give);
    }

    private void MoveGiveCursor(int delta)
    {
        if (_giveOptions.Count > 0) _giveCursor = Mod(_giveCursor + delta, _giveOptions.Count);
    }

    private void HandleGiveClick(Point point)
    {
        if (point.X is >= 14 and < 418 && point.Y is >= 110 and < 390)
        {
            var index = (point.Y - 110) / 56;
            if (index < _giveOptions.Count) _giveCursor = index;
        }
        else if (GiveQueue.Contains(point)) QueueSelectedGive();
        else if (GiveBack.Contains(point)) _screens.Show(ClientScreen.Items);
    }

    private void QueueSelectedGive()
    {
        if (_giveOptions.Count == 0 || _replay is null) return;
        var command = _giveOptions[_giveCursor];
        var result = _replay.Submit(command);
        _message = result.Accepted
            ? "GIVE QUEUED"
            : result.Validation.Message.ToUpperInvariant();
        if (result.Accepted) _screens.Show(ClientScreen.City);
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
        if (_choosingCommandTarget)
        {
            if (IsEquipmentCommandPicker() && _state is not null)
            {
                var indices = EquipmentCommandIndices(_state);
                if (indices.Count > 0)
                {
                    var position = indices.IndexOf(_commandTargetCursor);
                    _commandTargetCursor = indices[Mod(position + delta, indices.Count)];
                }
            }
            else if (_commandTargetOptions.Count > 0)
                _commandTargetCursor = Mod(_commandTargetCursor + delta, _commandTargetOptions.Count);
        }
        else
        {
            _commandCursor = Mod(_commandCursor + delta, CommandOverlayLayout.Actions.Count);
        }
    }

    private void HandleCommandsClick(Point point)
    {
        if (_choosingCommandTarget)
        {
            if (IsAttackCommandPicker())
            {
                HandleAttackCommandClick(point);
                return;
            }
            if (IsInfluenceCommandPicker())
            {
                if (InfluenceCommandLayout.Cancel.Contains(point))
                {
                    BackFromCommands();
                    return;
                }
                if (InfluenceCommandLayout.Ok.Contains(point))
                {
                    ActivateCommandSelection();
                    return;
                }
                for (var slot = 0; slot < MatchLimits.SitesPerSector; slot++)
                {
                    if (!InfluenceCommandLayout.Site(slot).Contains(point)) continue;
                    var actor = _state is null || _commandTargetOptions.Count == 0
                        ? null
                        : _state.FindGang(_commandTargetOptions[0].Gang);
                    if (actor is null) return;
                    var targetId = actor.SectorId * MatchLimits.SitesPerSector + slot;
                    var openDetails = _influenceSiteClicks.Register(targetId, _inputTime);
                    for (var candidateIndex = 0; candidateIndex < _commandTargetOptions.Count; candidateIndex++)
                    {
                        if (_commandTargetOptions[candidateIndex].Target.Id != targetId) continue;
                        _commandTargetCursor = candidateIndex;
                        if (openDetails) OpenSiteDetails(actor.SectorId, slot, ClientScreen.Commands);
                        return;
                    }
                    return;
                }
                if (!InfluenceCommandLayout.Panel.Contains(point)) BackFromCommands();
                return;
            }
            if (IsEquipmentCommandPicker())
            {
                if (EquipmentCommandLayout.Cancel.Contains(point))
                {
                    BackFromCommands();
                    return;
                }
                if (EquipmentCommandLayout.Ok.Contains(point))
                {
                    ActivateCommandSelection();
                    return;
                }
                for (var category = 0; category < EquipmentCommandLayout.CategoryCount; category++)
                {
                    if (!EquipmentCommandLayout.Category(category).Contains(point)) continue;
                    SelectEquipmentCategory(category);
                    return;
                }
                if (_state is null) return;
                var indices = EquipmentCommandIndices(_state);
                var position = indices.IndexOf(_commandTargetCursor);
                var itemFirst = Math.Max(0, position - 5);
                var itemVisible = Math.Min(12, indices.Count - itemFirst);
                var itemRow = Enumerable.Range(0, Math.Max(0, itemVisible))
                    .FirstOrDefault(row => EquipmentCommandLayout.ItemRow(row).Contains(point), -1);
                if (itemRow >= 0)
                {
                    _commandTargetCursor = indices[itemFirst + itemRow];
                    var itemId = _commandTargetOptions[_commandTargetCursor].Target.Id;
                    if (_equipmentItemClicks.Register(itemId, _inputTime)) OpenItemDetails((short)itemId);
                }
                else if (!EquipmentCommandLayout.Panel.Contains(point)) BackFromCommands();
                return;
            }
            var first = Math.Max(0, _commandTargetCursor - 6);
            var visible = Math.Min(13, _commandTargetOptions.Count - first);
            var index = Enumerable.Range(0, Math.Max(0, visible))
                .FirstOrDefault(row => CommandOverlayLayout.TargetRow(row).Contains(point), -1);
            if (index >= 0)
            {
                _commandTargetCursor = first + index;
                ActivateCommandSelection();
            }
            else if (!CommandOverlayLayout.TargetPanel.Contains(point)) BackFromCommands();
            return;
        }
        var actionIndex = Enumerable.Range(0, CommandOverlayLayout.Actions.Count)
            .FirstOrDefault(index => CommandOverlayLayout.ActionRow(index).Contains(point), -1);
        if (actionIndex >= 0)
        {
            _commandCursor = actionIndex;
            ActivateCommandSelection();
        }
        else if (!CommandOverlayLayout.Panel.Contains(point)) BackFromCommands();
    }

    private void ActivateCommandSelection()
    {
        if (_replay is null) return;
        if (_choosingCommandTarget)
        {
            if (IsEquipmentCommandPicker() && (_state is null
                || !EquipmentCommandIndices(_state).Contains(_commandTargetCursor)))
            {
                _message = "NO ITEMS IN THIS CATEGORY";
            }
            else if (_commandTargetOptions.Count > 0)
                SubmitCommand(_commandTargetOptions[_commandTargetCursor]);
            return;
        }

        var action = CommandOverlayLayout.Actions[_commandCursor];
        if (action == GangAction.None)
        {
            CancelSelectedCommand();
            return;
        }
        var options = _commandOptions.Where(command => command.Action == action).ToArray();
        if (options.Length == 0)
        {
            _message = $"{action.ToString().ToUpperInvariant()} IS NOT AVAILABLE";
            return;
        }
        if (CommandOverlayLayout.OpensTargetPicker(action))
        {
            _commandTargetOptions = options;
            _commandTargetCursor = 0;
            _equipmentCategory = 0;
            _choosingCommandTarget = true;
            if (action is GangAction.Equip or GangAction.Research)
                SelectEquipmentCategory(0);
            return;
        }
        SubmitCommand(options[0]);
    }

    private void SubmitCommand(GameCommand selection)
    {
        if (_replay is null) return;
        var command = selection with { Repeat = _commandRepeats };
        var result = _replay.Submit(command);
        _message = result.Accepted
            ? $"{(_commandRepeats ? "REPEATING " : "")}{command.Action.ToString().ToUpperInvariant()} QUEUED"
            : result.Validation.Message.ToUpperInvariant();
        if (result.Accepted) _screens.Show(_commandReturnScreen);
    }

    private void CancelSelectedCommand()
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId || _replay is null) return;
        var gang = SelectedGang(_state.FindPlayer(playerId)!);
        if (gang is null) return;
        var result = _replay.Cancel(playerId, gang.Id);
        _message = result.Accepted ? "COMMAND CLEARED" : result.Validation.Message.ToUpperInvariant();
        if (result.Accepted) _screens.Show(_commandReturnScreen);
    }

    private void BackFromCommands()
    {
        if (_choosingCommandTarget)
        {
            _choosingCommandTarget = false;
            _commandTargetOptions = [];
            return;
        }
        _screens.Show(_commandReturnScreen);
    }

    private void CycleGang(int delta)
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId) return;
        var gangs = _state.FindPlayer(playerId)!.Gangs.Where(gang => gang.IsActive).ToArray();
        if (gangs.Length == 0) return;
        _selectedGangIndex = Mod(_selectedGangIndex + delta, gangs.Length);
        _cursor = gangs[_selectedGangIndex].SectorId;
        if (_screens.Current == ClientScreen.Gang && _gangDetailsInstanceId is not null)
            _gangDetailsInstanceId = gangs[_selectedGangIndex].Id;
        _message = $"GANG {gangs[_selectedGangIndex].Id.Value}";
    }

    private MatchGangState? SelectedGang(MatchPlayerState player)
    {
        var gangs = player.Gangs.Where(gang => gang.IsActive).ToArray();
        if (gangs.Length == 0) return null;
        _selectedGangIndex = Math.Clamp(_selectedGangIndex, 0, gangs.Length - 1);
        return gangs[_selectedGangIndex];
    }

    private static int Mod(int value, int divisor) => (value % divisor + divisor) % divisor;

    private void DrawBoard(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        if (_cityBackground is not null)
            batch.Draw(_cityBackground, new Rectangle(0, 0, 640, 460), Color.White);
        if (_uiSprites is not null)
            foreach (var setupPlayer in state.Setup.Players)
                batch.Draw(_uiSprites, PlayerPortraitLayout.CityTop(setupPlayer.Id.Value),
                    OriginalSpriteLayout.OverlordPortrait(setupPlayer.PortraitId), Color.White);
        var playerIndex = state.Coordinator.ActivePlayer?.Value ?? 0;
        var player = state.Players[playerIndex];
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
            if (sector.CrackdownActive && _policeSprites is not null)
                batch.Draw(
                    _policeSprites,
                    new Rectangle(destination.X + 14, destination.Y + 10, 27, 32),
                    OriginalSpriteLayout.PolicePatrolCar,
                    Color.White);
            if (index == _cursor) DrawBorder(batch, pixel, destination, Color.Gold, 2);
        }
        foreach (var gangs in player.Gangs.Where(gang => gang.IsActive).GroupBy(gang => gang.SectorId))
            DrawGangStatusMarker(batch, gangs.Key,
                gangs.Any(gang => gang.QueuedCommand is not null)
                    ? OriginalSpriteLayout.AssignedGangStatus
                    : OriginalSpriteLayout.IdleGangStatus);
        foreach (var pending in player.PendingHires)
            DrawGangStatusMarker(batch, pending.TargetSectorId, OriginalSpriteLayout.IncomingGangStatus);
        if (_draggedHireDefinitionId is not null
            && CityMapLayout.TrySectorAt(_dragPoint, out var dropSector))
        {
            DrawGangStatusMarker(batch, dropSector, OriginalSpriteLayout.IncomingGangStatus);
            DrawBorder(batch, pixel, CityMapLayout.Destination(dropSector),
                state.Sectors[dropSector].Owner == player.Id ? Color.Lime : Color.OrangeRed, 2);
        }

        var selectedSector = state.Sectors[_cursor];
        var scenario = ScenarioCatalog.Get(state.Setup.Scenario);
        batch.Draw(pixel, new Rectangle(StatusConsoleLayout.LabelLeft, 14, 44, 8), Color.Black);
        font.Draw(batch, scenario.Name,
            new Vector2(StatusConsoleLayout.LabelLeft, StatusConsoleLayout.ScenarioY), Color.Lime, 1);
        font.Draw(batch, MatchDate(state.Coordinator.Turn),
            new Vector2(StatusConsoleLayout.LabelLeft, StatusConsoleLayout.DateY), Color.Lime, 1);
        DrawPanelValue(font, batch, ScenarioScore(state, player).ToString(),
            StatusConsoleLayout.ValueRight, StatusConsoleLayout.ScoreY);
        DrawPanelValue(font, batch, player.Cash,
            StatusConsoleLayout.ValueRight, StatusConsoleLayout.CashY);
        DrawPanelValue(font, batch, SectorCode(_cursor),
            StatusConsoleLayout.ValueRight, StatusConsoleLayout.SectorValueY(0));
        DrawPanelValue(font, batch, $"${SectorSiteIncome(state, selectedSector)}",
            StatusConsoleLayout.ValueRight, StatusConsoleLayout.SectorValueY(1));
        DrawPanelValue(font, batch, selectedSector.Tolerance,
            StatusConsoleLayout.ValueRight, StatusConsoleLayout.SectorValueY(2));
        DrawPanelValue(font, batch, SectorSupport(state, player.Id, selectedSector),
            StatusConsoleLayout.ValueRight, StatusConsoleLayout.SectorValueY(3));
        DrawPanelValue(font, batch, selectedSector.Chaos,
            StatusConsoleLayout.ValueRight, StatusConsoleLayout.SectorValueY(4));
        font.Draw(batch, _message.Length <= 32 ? _message : _message[..32],
            new Vector2(438, 354), Color.Gold, 1);
        DrawHireDock(batch, font, state, player);
        if (_hireDragStarted && _draggedHireDefinitionId is { } draggedDefinition && _gangPortraits is not null)
        {
            var token = new Rectangle(_dragPoint.X - 18, _dragPoint.Y - 18, 36, 36);
            batch.Draw(_gangPortraits, token,
                OriginalSpriteLayout.GangPortrait(draggedDefinition), Color.White);
            DrawBorder(batch, pixel, token, Color.White, 1);
        }
        font.Draw(batch, "ARROWS ENTER/H/SPACE  F5/F9 SAVE  F6/F10 REPLAY", new Vector2(18, 439), new Color(180, 190, 190), 1);
    }

    private void DrawGangStatusMarker(SpriteBatch batch, int sectorId, Rectangle source)
    {
        if (_uiSprites is not null)
            batch.Draw(_uiSprites, GangStatusMarkerLayout.Destination(sectorId), source, Color.White);
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
        DrawLastTurnEventsPanel(batch, pixel, font, state);
    }

    private void DrawCommands(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        if (_commandReturnScreen == ClientScreen.Sector) DrawSectorDetails(batch, pixel, font, state);
        else DrawBoard(batch, pixel, font, state);
        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        var gang = SelectedGang(state.FindPlayer(playerId)!);
        if (_choosingCommandTarget)
        {
            DrawCommandTargets(batch, pixel, font, state);
            return;
        }

        var panel = CommandOverlayLayout.Panel;
        batch.Draw(pixel, panel, new Color(12, 18, 18, 246));
        DrawBorder(batch, pixel, panel, new Color(0, 190, 65), 2);
        font.Draw(batch, _commandRepeats ? "RECURRING ACTION" : "ONE-OFF ACTION",
            new Vector2(panel.X + 8, panel.Y + 5), Color.Gold, 1);
        for (var index = 0; index < CommandOverlayLayout.Actions.Count; index++)
        {
            var action = CommandOverlayLayout.Actions[index];
            var row = CommandOverlayLayout.ActionRow(index);
            var available = action == GangAction.None
                ? gang?.QueuedCommand is not null
                : _commandOptions.Any(command => command.Action == action);
            if (index == _commandCursor)
                batch.Draw(pixel, row, new Color(65, 35, 25));
            if (action is GangAction.None or GangAction.Terminate)
                batch.Draw(pixel, new Rectangle(row.X, row.Y - 2, row.Width, 1), new Color(65, 95, 80));
            var label = action.ToString().ToUpperInvariant()
                + (CommandOverlayLayout.OpensTargetPicker(action) ? "..." : "");
            font.Draw(batch, label, new Vector2(row.X + 5, row.Y + 6),
                available ? Color.White : new Color(90, 105, 100), 1);
        }
    }

    private void DrawCommandTargets(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        if (IsEquipmentCommandPicker())
        {
            DrawEquipmentCommandTargets(batch, pixel, font, state);
            return;
        }
        if (IsInfluenceCommandPicker())
        {
            DrawInfluenceCommandTargets(batch, pixel, state);
            return;
        }
        if (IsAttackCommandPicker())
        {
            DrawAttackCommandTargets(batch, pixel, font, state);
            return;
        }
        var panel = CommandOverlayLayout.TargetPanel;
        batch.Draw(pixel, panel, new Color(12, 18, 18, 248));
        DrawBorder(batch, pixel, panel, new Color(0, 190, 65), 2);
        var action = _commandTargetOptions.Count == 0
            ? GangAction.None
            : _commandTargetOptions[_commandTargetCursor].Action;
        font.Draw(batch, action.ToString().ToUpperInvariant(),
            new Vector2(panel.X + 8, panel.Y + 8), Color.Gold, 1);
        var first = Math.Max(0, _commandTargetCursor - 6);
        foreach (var entry in _commandTargetOptions.Skip(first).Take(13)
                     .Select((command, row) => (command, row)))
        {
            var index = first + entry.row;
            var rectangle = CommandOverlayLayout.TargetRow(entry.row);
            if (index == _commandTargetCursor)
                batch.Draw(pixel, rectangle, new Color(65, 35, 25));
            var label = FormatCommandTargets(state, entry.command);
            if (label.Length > 30) label = label[..30];
            font.Draw(batch, label, new Vector2(rectangle.X + 4, rectangle.Y + 5), Color.White, 1);
        }
    }

    private bool IsEquipmentCommandPicker() => _commandTargetOptions.Count > 0
        && _commandTargetOptions[0].Action is GangAction.Equip or GangAction.Research;

    private bool IsInfluenceCommandPicker() => _commandTargetOptions.Count > 0
        && _commandTargetOptions[0].Action == GangAction.Influence;

    private bool IsAttackCommandPicker() => _commandTargetOptions.Count > 0
        && _commandTargetOptions[0].Action == GangAction.Attack;

    private void DrawInfluenceCommandTargets(
        SpriteBatch batch,
        Texture2D pixel,
        MatchState state)
    {
        if (_influenceBackground is not null)
            batch.Draw(_influenceBackground, InfluenceCommandLayout.Panel, Color.White);
        else
            batch.Draw(pixel, InfluenceCommandLayout.Panel, new Color(0, 0, 0, 248));

        var actor = state.FindGang(_commandTargetOptions[0].Gang)!;
        var actorDefinition = state.Definitions.Gangs.Single(gang => gang.Id == actor.DefinitionId);
        if (_gangPortraits is not null)
            batch.Draw(_gangPortraits, InfluenceCommandLayout.Portrait,
                OriginalSpriteLayout.GangPortrait(actorDefinition.Id), Color.White);

        var sector = state.Sectors[actor.SectorId];
        for (var slot = 0; slot < MatchLimits.SitesPerSector; slot++)
        {
            var site = sector.Sites.Single(value => value.Slot == slot);
            var destination = InfluenceCommandLayout.Site(slot);
            if (_sitePortraits is not null)
                batch.Draw(_sitePortraits, destination,
                    OriginalSpriteLayout.SitePortrait(site.DefinitionId), Color.White);
            var targetId = actor.SectorId * MatchLimits.SitesPerSector + slot;
            if (_commandTargetOptions[_commandTargetCursor].Target.Id == targetId)
                DrawBorder(batch, pixel, destination, Color.White, 2);
        }
    }

    private void DrawEquipmentCommandTargets(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        var action = _commandTargetOptions[0].Action;
        var background = action == GangAction.Equip
            ? _equipmentPurchaseBackground
            : _equipmentResearchBackground;
        if (background is not null)
            batch.Draw(background, EquipmentCommandLayout.Panel, Color.White);
        else
            batch.Draw(pixel, EquipmentCommandLayout.Panel, new Color(0, 0, 0, 248));
        var actor = state.FindGang(_commandTargetOptions[0].Gang)!;
        if (_gangPortraits is not null)
            batch.Draw(_gangPortraits, EquipmentCommandLayout.Portrait,
                OriginalSpriteLayout.GangPortrait(actor.DefinitionId), Color.White);

        var indices = EquipmentCommandIndices(state);
        var position = indices.IndexOf(_commandTargetCursor);
        var first = Math.Max(0, position - 5);
        foreach (var entry in indices.Skip(first).Take(12)
                     .Select((index, row) => (index, row)))
        {
            var command = _commandTargetOptions[entry.index];
            var item = state.Definitions.Items[command.Target.Id];
            var rectangle = EquipmentCommandLayout.ItemRow(entry.row);
            if (entry.index == _commandTargetCursor)
                batch.Draw(pixel, rectangle, new Color(55, 65, 25));
            font.Draw(batch, item.Name, new Vector2(rectangle.X + 2, rectangle.Y + 1), Color.Lime, 1);
            var value = action == GangAction.Equip
                ? SpecialSiteRules.EquipmentCost(state, actor, item)
                : item.ResearchDifficulty;
            font.Draw(batch, value.ToString(), new Vector2(rectangle.Right - 12, rectangle.Y + 1),
                Color.Lime, 1);
        }
        DrawBorder(batch, pixel, EquipmentCommandLayout.Category(_equipmentCategory), Color.White, 2);
    }

    private List<int> EquipmentCommandIndices(MatchState state) => Enumerable.Range(0, _commandTargetOptions.Count)
        .Where(index => EquipmentCommandLayout.CategoryForItemType(
            state.Definitions.Items[_commandTargetOptions[index].Target.Id].Type) == _equipmentCategory)
        .ToList();

    private void SelectEquipmentCategory(int category)
    {
        if (category is < 0 or >= EquipmentCommandLayout.CategoryCount)
            throw new ArgumentOutOfRangeException(nameof(category));
        _equipmentCategory = category;
        if (_state is null) return;
        var indices = EquipmentCommandIndices(_state);
        if (indices.Count > 0) _commandTargetCursor = indices[0];
    }

    private void DrawSectorDetails(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        DrawBoard(batch, pixel, font, state);
        batch.Draw(pixel, new Rectangle(0, 42, 438, 418), Color.Black);
        DrawSectorSideRail(batch, pixel, font);
        var sector = state.Sectors[_cursor];
        DrawSectorNeighborhood(batch, pixel, font, state);
        foreach (var site in sector.Sites)
        {
            var definition = state.Definitions.Sites.Single(value => value.Id == site.DefinitionId);
            var portrait = SectorDetailLayout.SitePortrait(site.Slot);
            if (_sitePortraits is not null)
                batch.Draw(_sitePortraits, portrait,
                    OriginalSpriteLayout.SitePortrait(definition.Id), Color.White);
            DrawBorder(batch, pixel, portrait,
                site.InfluencedBy is { } influencedBy ? PlayerColors[influencedBy.Value] : Color.Gray, 1);
            var control = SectorDetailLayout.SiteControlBar(site.Slot);
            batch.Draw(pixel, control, Color.Red);
            var controlled = definition.Resistance == 0
                ? control.Width
                : (int)Math.Round(control.Width
                    * (definition.Resistance - Math.Clamp(site.Resistance, 0, definition.Resistance))
                    / (double)definition.Resistance);
            if (controlled > 0)
                batch.Draw(pixel, new Rectangle(control.X, control.Y, controlled, control.Height), Color.Lime);
        }
        var viewer = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        var visibleGangs = SectorGangView.Visible(state, viewer, sector.Id)
            .OrderBy(gang => gang.Owner == viewer ? 0 : 1)
            .ThenBy(gang => gang.Id.Value)
            .ToArray();
        foreach (var entry in visibleGangs.Take(SectorGangCardLayout.VisibleCards)
                     .Select((gang, index) => (gang, index)))
            DrawSectorGangCard(batch, pixel, font, state, viewer, entry.gang, entry.index);
        if (visibleGangs.Length > SectorGangCardLayout.VisibleCards)
            font.Draw(batch, $"+{visibleGangs.Length - SectorGangCardLayout.VisibleCards}",
                new Vector2(397, 123), Color.White, 1);
        DrawGangMoveDrag(batch, pixel, state);
        DrawSectorHireDrag(batch, pixel, state);
    }

    private void DrawSectorHireDrag(SpriteBatch batch, Texture2D pixel, MatchState state)
    {
        if (!_hireDragStarted || _draggedHireDefinitionId is not { } definitionId) return;
        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        var overMap = SectorDetailLayout.TrySectorAt(_dragPoint, _cursor, out var dropSector);
        if (!overMap && SectorDetailLayout.Workspace.Contains(_dragPoint)) dropSector = _cursor;
        if (overMap)
        {
            var marker = SectorDetailLayout.Marker(_cursor, dropSector);
            if (marker is { } destination && _uiSprites is not null)
                batch.Draw(_uiSprites, destination, OriginalSpriteLayout.IncomingGangStatus, Color.White);
            for (var column = 0; column < SectorDetailLayout.Columns; column++)
            for (var row = 0; row < SectorDetailLayout.Rows; row++)
                if (SectorDetailLayout.SectorAt(_cursor, column, row) == dropSector)
                    DrawBorder(batch, pixel, SectorDetailLayout.Cell(column, row),
                        state.Sectors[dropSector].Owner == playerId ? Color.Lime : Color.OrangeRed, 2);
        }
        else if (SectorDetailLayout.Workspace.Contains(_dragPoint))
        {
            DrawBorder(batch, pixel, SectorDetailLayout.Workspace,
                state.Sectors[_cursor].Owner == playerId ? Color.Lime : Color.OrangeRed, 2);
        }
        if (_gangPortraits is null) return;
        var token = new Rectangle(_dragPoint.X - 18, _dragPoint.Y - 18, 36, 36);
        batch.Draw(_gangPortraits, token,
            OriginalSpriteLayout.GangPortrait(definitionId), Color.White);
        DrawBorder(batch, pixel, token, Color.White, 1);
    }

    private void BeginGangDrag(MatchGangState gang, Point point)
    {
        _draggedGangId = gang.Id;
        _gangPressPoint = point;
        _gangDragStarted = false;
        _dragPoint = point;
        _message = "DRAG TO MOVE; DOUBLE-CLICK FOR DETAILS";
    }

    private void CompleteGangClick()
    {
        var gangId = _draggedGangId;
        _draggedGangId = null;
        _gangDragStarted = false;
        if (gangId is null || _state?.FindGang(gangId.Value) is not { } gang) return;
        if (_sectorGangClicks.Register(gang.Id.Value, _inputTime))
            OpenGangDetails(gang, ClientScreen.Sector);
        else
            _message = "DOUBLE-CLICK FOR DETAILS OR DRAG TO MOVE";
    }

    private void CompleteGangDrag(Point point)
    {
        var gangId = _draggedGangId;
        _draggedGangId = null;
        _gangDragStarted = false;
        if (gangId is null || _state?.FindGang(gangId.Value) is not { } gang || _replay is null) return;
        if (!SectorDetailLayout.TrySectorAt(point, _cursor, out var sectorId))
        {
            _message = "MOVE CANCELLED";
            return;
        }
        var legal = CommandOptionCatalog.LegalCommands(_state, gang.Owner, gang.Id)
            .FirstOrDefault(command => command.Action == GangAction.Move
                && command.Target == CommandTarget.Sector(sectorId));
        if (legal is null)
        {
            _message = "MOVE REQUIRES A NEIGHBORING SECTOR";
            return;
        }
        var result = _replay.Submit(legal with { Repeat = false });
        _message = result.Accepted
            ? $"MOVE TO SECTOR {sectorId + 1} QUEUED"
            : result.Validation.Message.ToUpperInvariant();
    }

    private void CancelGangDrag()
    {
        _draggedGangId = null;
        _gangDragStarted = false;
        _message = "MOVE CANCELLED";
    }

    private void DrawGangMoveDrag(SpriteBatch batch, Texture2D pixel, MatchState state)
    {
        if (!_gangDragStarted || _draggedGangId is not { } gangId || state.FindGang(gangId) is not { } gang)
            return;
        var legalSectors = CommandOptionCatalog.LegalCommands(state, gang.Owner, gang.Id)
            .Where(command => command.Action == GangAction.Move)
            .Select(command => command.Target.Id)
            .ToHashSet();
        for (var column = 0; column < SectorDetailLayout.Columns; column++)
        for (var row = 0; row < SectorDetailLayout.Rows; row++)
        {
            if (SectorDetailLayout.SectorAt(_cursor, column, row) is not { } sectorId
                || !legalSectors.Contains(sectorId)) continue;
            DrawBorder(batch, pixel, SectorDetailLayout.Cell(column, row), Color.Lime, 2);
        }
        if (_gangPortraits is null) return;
        var token = new Rectangle(_dragPoint.X - 18, _dragPoint.Y - 14, 36, 28);
        batch.Draw(_gangPortraits, token, OriginalSpriteLayout.GangPortrait(gang.DefinitionId), Color.White);
        DrawBorder(batch, pixel, token, PlayerColors[gang.Owner.Value], 1);
    }

    private void DrawSectorSideRail(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        var rail = new Rectangle(3, 43, 29, 417);
        batch.Draw(pixel, rail, new Color(105, 105, 105));
        DrawBorder(batch, pixel, rail, new Color(185, 185, 185), 1);
        batch.Draw(pixel, new Rectangle(6, 46, 22, 99), new Color(0, 180, 20));
        batch.Draw(pixel, new Rectangle(8, 48, 18, 95), new Color(210, 0, 0));
        batch.Draw(pixel, new Rectangle(6, 146, 22, 248), Color.Black);
        for (var y = 150; y < 394; y += 8)
            batch.Draw(pixel, new Rectangle(7, y, 20, 1), new Color(0, 20, 115));
        if (_uiSprites is not null)
            batch.Draw(_uiSprites, SectorDetailLayout.Back,
                OriginalSpriteLayout.SectorBackArrow, Color.White);
        else
            font.Draw(batch, "<", new Vector2(8, 410), Color.Lime, 2);
    }

    private void DrawSectorGangCard(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state,
        PlayerId viewer,
        MatchGangState gang,
        int slot)
    {
        var frame = SectorGangCardLayout.Frame(slot);
        if (_uiSprites is not null)
            batch.Draw(_uiSprites, frame, OriginalSpriteLayout.GangCardFrame, Color.White);
        else
        {
            batch.Draw(pixel, frame, new Color(115, 115, 115));
            batch.Draw(pixel, new Rectangle(frame.X + 3, frame.Y + 3, frame.Width - 6, frame.Height - 7), Color.Black);
        }
        DrawBorder(batch, pixel, frame, PlayerColors[gang.Owner.Value], 2);

        var force = SectorGangCardLayout.ForceBar(slot);
        batch.Draw(pixel, force, new Color(45, 45, 45));
        var forceWidth = Math.Clamp((force.Width * gang.Force + 9) / 10, 0, force.Width);
        if (forceWidth > 0)
            batch.Draw(pixel, new Rectangle(force.X, force.Y, forceWidth, force.Height),
                Color.Lime);

        var controlsEnabled = gang.Owner == viewer;
        if (gang.QueuedCommand is { } queued)
        {
            var assigned = SectorGangCardLayout.AssignedCommand(slot);
            batch.Draw(pixel, assigned, new Color(20, 28, 25));
            DrawBorder(batch, pixel, assigned,
                controlsEnabled ? PlayerColors[gang.Owner.Value] : Color.Gray, 1);
            var label = queued.Command.Action.ToString().ToUpperInvariant();
            var x = assigned.Center.X - label.Length * 3;
            font.Draw(batch, label, new Vector2(x, assigned.Y + 4),
                controlsEnabled ? Color.Lime : Color.Gray, 1);
        }
        else
        {
            var once = SectorGangCardLayout.OneOffAction(slot);
            var repeat = SectorGangCardLayout.RepeatingAction(slot);
            batch.Draw(pixel, once, new Color(34, 34, 34));
            batch.Draw(pixel, repeat, new Color(34, 34, 34));
            DrawBorder(batch, pixel, once, Color.LightGray, 1);
            DrawBorder(batch, pixel, repeat, Color.LightGray, 1);
            DrawDownArrow(batch, pixel, once.Center.X, once.Y + 5,
                controlsEnabled ? Color.Lime : Color.Gray);
            DrawDownArrow(batch, pixel, repeat.Center.X, repeat.Y + 3,
                controlsEnabled ? Color.Lime : Color.Gray, compact: true);
            DrawDownArrow(batch, pixel, repeat.Center.X, repeat.Y + 9,
                controlsEnabled ? Color.Lime : Color.Gray, compact: true);
        }

        var definition = state.Definitions.Gangs.Single(value => value.Id == gang.DefinitionId);
        if (_gangPortraits is not null)
            batch.Draw(_gangPortraits, SectorGangCardLayout.Portrait(slot),
                OriginalSpriteLayout.GangPortrait(definition.Id), Color.White);
        for (var itemSlot = 0; itemSlot < 3; itemSlot++)
        {
            // The source frame also contains legacy pixels in this strip. The
            // live card footer is exclusively the gang's three equipment slots.
            var item = SectorGangCardLayout.ItemSlot(slot, itemSlot);
            batch.Draw(pixel, item, Color.Black);
            DrawBorder(batch, pixel, item, Color.LightGray, 1);
        }
    }

    private static void DrawDownArrow(
        SpriteBatch batch,
        Texture2D pixel,
        int centerX,
        int top,
        Color color,
        bool compact = false)
    {
        var widths = compact ? new[] { 7, 5, 3, 1 } : new[] { 9, 7, 5, 3, 1 };
        for (var row = 0; row < widths.Length; row++)
            batch.Draw(pixel, new Rectangle(centerX - widths[row] / 2, top + row, widths[row], 1), color);
    }

    private static void DrawHorizontalArrow(
        SpriteBatch batch,
        Texture2D pixel,
        Rectangle bounds,
        bool left,
        Color color)
    {
        for (var offset = 0; offset < 7; offset++)
        {
            var height = 1 + offset * 2;
            var x = left ? bounds.X + 2 + offset : bounds.Right - 3 - offset;
            batch.Draw(pixel, new Rectangle(x, bounds.Center.Y - height / 2, 1, height), color);
        }
    }

    private void DrawSectorNeighborhood(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        for (var row = 0; row < SectorDetailLayout.Rows; row++)
        for (var column = 0; column < SectorDetailLayout.Columns; column++)
        {
            var destination = SectorDetailLayout.Cell(column, row);
            if (SectorDetailLayout.SectorAt(_cursor, column, row) is not { } sectorId)
            {
                batch.Draw(pixel, destination, Color.Black);
                DrawBorder(batch, pixel, destination, new Color(0, 110, 30), 1);
                continue;
            }
            var sector = state.Sectors[sectorId];
            var layer = _cityOwnershipLayers[CityMapLayout.OwnershipSheet(sector.Owner)];
            if (layer is not null)
                batch.Draw(layer, destination, CityMapLayout.Source(sectorId), Color.White);
            else
                batch.Draw(pixel, destination, new Color(24, 37, 39));
            if (sector.CrackdownActive && _policeSprites is not null)
                batch.Draw(_policeSprites,
                    new Rectangle(destination.X + 14, destination.Y + 10, 27, 32),
                    OriginalSpriteLayout.PolicePatrolCar, Color.White);
            DrawBorder(batch, pixel, destination,
                column == 1 && row == 1 ? Color.White : new Color(0, 150, 45),
                column == 1 && row == 1 ? 2 : 1);
            if (row == 0)
                DrawSectorCoordinateBadge(batch, pixel, font,
                    new Point(destination.Center.X, destination.Y + 1),
                    ((char)('A' + sectorId % 8)).ToString(), top: true);
            if (column == 0)
                DrawSectorCoordinateBadge(batch, pixel, font,
                    new Point(destination.X + 1, destination.Center.Y),
                    (sectorId / 8 + 1).ToString(), top: false);
        }

        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        var player = state.FindPlayer(playerId)!;
        foreach (var gang in player.Gangs.Where(gang => gang.IsActive))
            if (SectorDetailLayout.Marker(_cursor, gang.SectorId) is { } gangMarker)
                DrawGangStatusMarker(batch, gangMarker,
                    gang.QueuedCommand is null
                        ? OriginalSpriteLayout.IdleGangStatus
                        : OriginalSpriteLayout.AssignedGangStatus);
        foreach (var pending in player.PendingHires)
            if (SectorDetailLayout.Marker(_cursor, pending.TargetSectorId) is { } hireMarker)
                DrawGangStatusMarker(batch, hireMarker, OriginalSpriteLayout.IncomingGangStatus);
    }

    private void DrawGangStatusMarker(SpriteBatch batch, Rectangle destination, Rectangle source)
    {
        if (_uiSprites is not null) batch.Draw(_uiSprites, destination, source, Color.White);
    }

    private static void DrawSectorCoordinateBadge(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        Point anchor,
        string label,
        bool top)
    {
        var bounds = top
            ? new Rectangle(anchor.X - 8, anchor.Y - 2, 16, 15)
            : new Rectangle(anchor.X - 3, anchor.Y - 8, 15, 16);
        batch.Draw(pixel, bounds, new Color(105, 105, 105));
        batch.Draw(pixel, new Rectangle(bounds.X + 2, bounds.Y + 1, bounds.Width - 4, bounds.Height - 3),
            Color.Black);
        batch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, 2, 2), Color.Black);
        batch.Draw(pixel, new Rectangle(bounds.Right - 2, bounds.Y, 2, 2), Color.Black);
        batch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - 2, 2, 2), Color.Black);
        batch.Draw(pixel, new Rectangle(bounds.Right - 2, bounds.Bottom - 2, 2, 2), Color.Black);
        font.Draw(batch, label, new Vector2(bounds.X + 5, bounds.Y + 4), Color.Lime, 1);
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
        DrawCombatResultsPanel(batch, pixel, font, state);
    }

    private void DrawCombatAnimation(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        DrawCombatPanel(batch, pixel, font, state);
    }

    private void DrawCombatGangPortrait(
        SpriteBatch batch,
        Texture2D pixel,
        MatchState state,
        GangId? gangId,
        Rectangle destination)
    {
        if (gangId is not { } id || state.FindGang(id) is not { } gang) return;
        if (_gangPortraits is not null)
            batch.Draw(_gangPortraits, destination,
                OriginalSpriteLayout.GangPortrait(gang.DefinitionId), Color.White);
        DrawBorder(batch, pixel, destination, PlayerColors[gang.Owner.Value], 1);
    }

    private void DrawSearch(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        DrawManagementPanel(batch, pixel);
        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        font.Draw(batch, $"SEARCH SECTOR {_cursor + 1}", new Vector2(18, 60), Color.Gold, 2);
        font.Draw(batch, state.FindPlayer(playerId)!.Setup.Name, new Vector2(18, 86),
            PlayerColors[playerId.Value], 1);
        var visible = SectorGangView.Visible(state, playerId, _cursor);
        if (visible.Count == 0)
            font.Draw(batch, "NO GANGS DETECTED", new Vector2(18, 116), Color.White, 1);
        foreach (var entry in visible.Take(SectorGangView.MaximumSearchRows)
                     .Select((gang, index) => (gang, index)))
        {
            var gang = entry.gang;
            var definition = state.Definitions.Gangs.Single(value => value.Id == gang.DefinitionId);
            var owner = state.FindPlayer(gang.Owner)!;
            var y = 112 + entry.index * 40;
            var portrait = SectorGangView.SearchPortrait(entry.index);
            if (_gangPortraits is not null)
                batch.Draw(_gangPortraits, portrait,
                    OriginalSpriteLayout.GangPortrait(definition.Id), Color.White);
            DrawBorder(batch, pixel, portrait, PlayerColors[gang.Owner.Value], 1);
            font.Draw(batch, definition.Name, new Vector2(62, y), PlayerColors[gang.Owner.Value], 1);
            font.Draw(batch, $"{owner.Setup.Name}  FORCE {gang.Force}"
                + (gang.Hidden ? "  HIDDEN" : ""), new Vector2(190, y), Color.White, 1);
        }
        if (visible.Count > SectorGangView.MaximumSearchRows)
            font.Draw(batch, $"+{visible.Count - SectorGangView.MaximumSearchRows} MORE",
                new Vector2(18, 390), Color.White, 1);
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
        if (gang is not null)
        {
            var portrait = GangArtLayout.SelectedEquipmentPortrait;
            if (_gangPortraits is not null)
                batch.Draw(_gangPortraits, portrait,
                    OriginalSpriteLayout.GangPortrait(gang.DefinitionId), Color.White);
            DrawBorder(batch, pixel, portrait, PlayerColors[playerId.Value], 1);
        }

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
            var equipmentCost = gang is null ? item.Cost : SpecialSiteRules.EquipmentCost(state, gang, item);
            font.Draw(batch, item.Name, new Vector2(350, 112), Color.Gold, 1);
            font.Draw(batch, $"{EquipmentRules.SlotFor(item).ToString().ToUpperInvariant()}  TECH {item.TechLevel}",
                new Vector2(350, 136), Color.White, 1);
            font.Draw(batch, $"COST ${equipmentCost}" + (equipmentCost < item.Cost ? "  FACTORY" : ""),
                new Vector2(350, 152), Color.White, 1);
            font.Draw(batch, $"RESEARCH {researched}", new Vector2(350, 168), Color.White, 1);
            if (gang is not null)
                font.Draw(batch, $"TECH LIMIT {SpecialSiteRules.ResearchTechLimit(state, gang)}",
                    new Vector2(350, 184), Color.White, 1);
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
        DrawButton(batch, pixel, font, ItemsGive, "GIVE", false);
        DrawButton(batch, pixel, font, ItemsSell, "SELL", false);
        DrawButton(batch, pixel, font, ItemsBack, "BACK", false);
    }

    private void DrawGiveTargets(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        if (_cityBackground is not null)
            batch.Draw(_cityBackground, new Rectangle(0, 0, 640, 460), Color.White);
        batch.Draw(pixel, new Rectangle(8, 48, 420, 402), new Color(0, 0, 0, 240));
        font.Draw(batch, "GIVE EQUIPMENT", new Vector2(18, 60), Color.Gold, 2);
        if (_giveOptions.Count == 0)
        {
            font.Draw(batch, "NO LEGAL RECIPIENT", new Vector2(18, 92), Color.White, 1);
        }
        else
        {
            var selected = _giveOptions[Math.Clamp(_giveCursor, 0, _giveOptions.Count - 1)];
            var actor = state.FindGang(selected.Gang)!;
            var item = state.Definitions.Items[selected.SecondaryTarget!.Value.Id];
            var actorName = state.Definitions.Gangs.Single(value => value.Id == actor.DefinitionId).Name;
            font.Draw(batch, $"{actorName} GIVES {item.Name}", new Vector2(18, 86), Color.White, 1);
            foreach (var entry in _giveOptions.Take(5).Select((command, index) => (command, index)))
            {
                var recipient = state.FindGang(new GangId(entry.command.Target.Id))!;
                var definition = state.Definitions.Gangs.Single(value => value.Id == recipient.DefinitionId);
                var y = 116 + entry.index * 56;
                if (entry.index == _giveCursor)
                    batch.Draw(pixel, new Rectangle(14, y - 6, 404, 50), new Color(72, 54, 18));
                if (_gangPortraits is not null)
                    batch.Draw(_gangPortraits, new Rectangle(20, y - 5, 42, 42),
                        OriginalSpriteLayout.GangPortrait(definition.Id), Color.White);
                font.Draw(batch, definition.Name, new Vector2(78, y), Color.White, 1);
                font.Draw(batch, $"FORCE {recipient.Force}  SECTOR {recipient.SectorId + 1}",
                    new Vector2(78, y + 16), new Color(180, 230, 170), 1);
            }
        }
        font.Draw(batch, "UP/DOWN RECIPIENT  ENTER GIVE", new Vector2(18, 395), Color.White, 1);
        DrawButton(batch, pixel, font, GiveQueue, "GIVE", false);
        DrawButton(batch, pixel, font, GiveBack, "BACK", false);
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

    private static string FormatCommandTargets(MatchState state, GameCommand command)
    {
        var text = command.Target.Kind == CommandTargetKind.None
            ? command.Action.ToString().ToUpperInvariant()
            : FormatTarget(state, command.Target);
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

    private static int SectorSiteIncome(MatchState state, MatchSectorState sector) => sector.Income;

    private static string SectorCode(int sectorId) =>
        $"{(char)('A' + sectorId % 8)}{sectorId / 8 + 1}";

    private static string MatchDate(int turn)
    {
        var week = Math.Max(0, turn - 1);
        return $"{2050 + week / 52}.{week % 52 + 1:00}";
    }

    private static long ScenarioScore(MatchState state, MatchPlayerState player)
    {
        var definition = ScenarioCatalog.Get(state.Setup.Scenario);
        return definition.IsTimed
            ? ScenarioCatalog.TimedScore(state.Setup.Scenario, state.Setup.Duration,
                MatchOutcomeEvaluator.Project(state, player))
            : 0;
    }

    private static int SectorSupport(MatchState state, PlayerId player, MatchSectorState sector) =>
        sector.Sites.Where(site => site.InfluencedBy == player)
            .Sum(site => state.Definitions.Sites.Single(definition => definition.Id == site.DefinitionId).Support);

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

}

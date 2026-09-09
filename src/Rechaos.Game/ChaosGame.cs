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
        Window.Title = "Chaos Overlords: New Chrome";
        _computerPlayers[1] = true;
    }

    protected override void LoadContent()
    {
        ValidateAssetPack();
        _batch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
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
        _font = _uiSprites is null
            ? throw new InvalidDataException("PX00129 is required for the original UI font.")
            : new PixelFont(GraphicsDevice, _uiSprites);
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

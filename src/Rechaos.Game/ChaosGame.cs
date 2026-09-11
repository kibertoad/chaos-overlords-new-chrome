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
    private static readonly Rectangle TitleOptions = new(196, 376, 80, 34);
    private static readonly Rectangle TitleHelp = new(280, 376, 80, 34);
    private static readonly Rectangle TitleQuit = new(364, 376, 80, 34);
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
    private static readonly Rectangle[] SetupAiMentalities =
    [
        new(80, 330, 108, 27), new(80, 359, 108, 27),
        new(80, 388, 108, 27), new(80, 417, 108, 27)
    ];
    private static readonly Rectangle ManagementBack = new(322, 414, 96, 28);
    private readonly GraphicsDeviceManager _graphics;
    private readonly string _assetRoot;
    private readonly string _quickSavePath;
    private readonly string _autoSavePath;
    private readonly string _replayPath;
    private readonly string _preferencesPath;
    private readonly bool _debugPhaseStepping;
    private readonly RuntimeDiagnostics? _diagnostics;
    private SpriteBatch? _batch;
    private Texture2D? _pixel;
    private Texture2D? _titleBackground;
    private Texture2D? _setupBackground;
    private Texture2D? _setupControls;
    private Texture2D? _cityBackground;
    private readonly Texture2D?[] _cityOwnershipLayers = new Texture2D?[MatchLimits.PlayerCount + 1];
    private Texture2D? _gameInfoBackground;
    private Texture2D? _idleGangWarningBackground;
    private Texture2D? _cityFinanceBackground;
    private Texture2D? _sectorFinanceBackground;
    private Texture2D? _rankingBackground;
    private Texture2D? _gangInfoBackground;
    private Texture2D? _gangDefinitionInfoBackground;
    private Texture2D? _siteInfoBackground;
    private Texture2D? _itemInfoBackground;
    private Texture2D? _combatBackground;
    private Texture2D? _combatResultsBackground;
    private Texture2D? _lastTurnEventsBackground;
    private Texture2D? _comlinkViewBackground;
    private Texture2D? _comlinkSendBackground;
    private readonly Texture2D?[] _lastTurnEventArtwork = new Texture2D?[10];
    private Texture2D? _hireComparisonBackground;
    private Texture2D? _influenceBackground;
    private Texture2D? _targetAcquisitionBackground;
    private Texture2D? _equipmentPurchaseBackground;
    private Texture2D? _equipmentResearchBackground;
    private Texture2D? _equipmentSellBackground;
    private Texture2D? _equipmentGiveBackground;
    private Texture2D? _movementBackground;
    private Texture2D? _sitePortraits;
    private Texture2D? _gangPortraits;
    private Texture2D? _itemPortraits;
    private Texture2D? _policeSprites;
    private Texture2D? _uiSprites;
    private PixelFont? _font;
    private readonly Dictionary<short, SoundEffect> _combatSounds = [];
    private readonly Dictionary<int, SoundEffect> _generalSounds = [];
    private readonly Dictionary<string, Texture2D> _combatAnimationTextures = [];
    private readonly CombatAnimationPlayer _combatAnimationPlayer = new();
    private readonly PanelSlideTransition _panelSlideTransition = new();
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
    private readonly short[] _playerPortraits = Enumerable.Range(0, MatchLimits.PlayerCount)
        .Select(index => checked((short)index)).ToArray();
    private readonly string[] _playerNames = Enumerable.Range(0, MatchLimits.PlayerCount)
        .Select(LocalSetupPolicy.DefaultPlayerName).ToArray();
    private readonly SetupPlayerNameEditor _setupNameEditor = new();
    private readonly LocalSetupRoster _localSetupRoster = new();
    private int? _editingPlayerName;
    private string _setupOriginalName = string.Empty;
    private AiDifficulty _selectedAiMentality = AiDifficulty.Criminal;
    private ScenarioId _selectedScenario = ScenarioId.Greed;
    private GameDuration _selectedDuration = GameDuration.SixMonths;
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
    private FinanceScope _financeScope = FinanceScope.City;
    private int _comlinkCursor;
    private readonly bool[] _comlinkRecipients = new bool[MatchLimits.PlayerCount];
    private readonly ComlinkTextEditor _comlinkEditor = new();
    private string _comlinkStatus = string.Empty;
    private IReadOnlyList<GameCommand> _giveOptions = [];
    private int _giveCursor;
    private readonly bool[] _giveSelections = new bool[3];
    private GangId? _giveGang;
    private ClientScreen _giveReturnScreen = ClientScreen.Items;
    private bool _giveRepeats;
    private readonly bool[] _sellSelections = new bool[3];
    private GangId? _sellGang;
    private ClientScreen _sellReturnScreen = ClientScreen.Items;
    private bool _sellRepeats;
    private int? _draggedHireSlot;
    private int? _draggedSetupPlayerSlot;
    private SetupPushButton? _pressedSetupButton;
    private short? _draggedHireDefinitionId;
    private Point _hirePressPoint;
    private Point _setupPlayerPressPoint;
    private bool _hireDragStarted;
    private bool _setupPlayerDragStarted;
    private GangId? _draggedGangId;
    private Point _gangPressPoint;
    private bool _gangDragStarted;
    private Point _dragPoint;
    private Point? _hoverPoint;
    private string _message = string.Empty;
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;
    private long _lastAnimatedEventSequence = -1;
    private TimeSpan _inputTime;
    private ClientScreen _gangDetailsReturnScreen = ClientScreen.City;
    private GangId? _gangDetailsInstanceId;
    private short? _gangDetailsDefinitionId;
    private int? _gangDetailsSectorFilter;
    private ClientScreen _siteDetailsReturnScreen = ClientScreen.Sector;
    private int? _siteDetailsSectorId;
    private int? _siteDetailsSlot;
    private short? _itemDetailsId;

    public ChaosGame(
        string assetRoot,
        bool debugPhaseStepping = false,
        RuntimeDiagnostics? diagnostics = null)
    {
        _assetRoot = assetRoot;
        _debugPhaseStepping = debugPhaseStepping;
        _diagnostics = diagnostics;
        _screens.Changed += (previous, current) => _diagnostics?.Write(
            "screen.changed",
            new Dictionary<string, string?>
            {
                ["from"] = previous.ToString(),
                ["to"] = current.ToString()
            });
        _screens.Changed += (previous, current) =>
        {
            if (_slidePanels) _panelSlideTransition.Begin(current, _inputTime);
            foreach (var slot in AudioRouting.PanelTransitionSounds(previous, current, _slidePanels))
                PlayGeneralSound(slot);
        };
        var userDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Rechaos Overlords");
        _quickSavePath = Path.Combine(userDataRoot, "quicksave.rchsave");
        _autoSavePath = Path.Combine(userDataRoot, "autosave.rchsave");
        _replayPath = Path.Combine(userDataRoot, "last-match.rchreplay");
        _preferencesPath = Path.Combine(userDataRoot, "preferences.json");
        var preferences = GamePreferencesStore.LoadOrDefault(_preferencesPath);
        _musicVolumeLevel = preferences.MusicVolumeLevel;
        _soundEffectVolumeLevel = preferences.SoundEffectVolumeLevel;
        _warnIfIdleGangs = preferences.WarnIfIdleGangs;
        _selectedPlanningTimeLimit = preferences.PlanningTimeLimit;
        _showBaseStatistics = preferences.ShowBaseStatistics;
        _detailedCombat = preferences.DetailedCombat;
        _slidePanels = preferences.SlidePanels;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 920,
            SynchronizeWithVerticalRetrace = true
        };
        IsMouseVisible = true;
        Window.Title = "Chaos Overlords: New Chrome";
    }

    protected override void LoadContent()
    {
        ValidateAssetPack();
        _batch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
        _definitions = BundledOriginalData.Load();
        _helpDocument = ExtractedHelpStore.LoadOrNull(_assetRoot);

        _titleBackground = LoadTexture("PX00130.bmp");
        _setupBackground = LoadTexture("PX00143.bmp");
        _setupControls = LoadTexture("PX00140.bmp");
        _cityBackground = LoadTexture("PX00128.bmp");
        for (var index = 0; index < _cityOwnershipLayers.Length; index++)
            _cityOwnershipLayers[index] = LoadTexture($"PX1000{index}.bmp");
        _endgameBackground = LoadTexture("PX00200.bmp");
        _gameInfoBackground = LoadTexture("PX05021.bmp");
        _idleGangWarningBackground = LoadTexture("PX05020.bmp");
        _cityFinanceBackground = LoadTexture("PX05008.bmp");
        _sectorFinanceBackground = LoadTexture("PX05019.bmp");
        _rankingBackground = LoadTexture("PX05011.bmp");
        _handoffPanel = LoadTexture("PX00132.bmp");
        _gangInfoBackground = LoadTexture("PX05000.bmp");
        _gangDefinitionInfoBackground = LoadTexture("PX05022.bmp");
        _siteInfoBackground = LoadTexture("PX05002.bmp");
        _itemInfoBackground = LoadTexture("PX05001.bmp");
        _combatBackground = LoadTexture("PX05014.bmp");
        _combatResultsBackground = LoadTexture("PX05012.bmp");
        _lastTurnEventsBackground = LoadTexture("PX05010.bmp");
        _comlinkViewBackground = LoadTexture("PX05017.bmp");
        _comlinkSendBackground = LoadTexture("PX05018.bmp");
        for (var eventArt = 1; eventArt <= 9; eventArt++)
            _lastTurnEventArtwork[eventArt] = LoadTexture($"PX060{eventArt:00}.bmp");
        _hireComparisonBackground = LoadTexture("PX05016.bmp");
        _influenceBackground = LoadTexture("PX05005.bmp");
        _targetAcquisitionBackground = LoadTexture("PX05003.bmp");
        _equipmentPurchaseBackground = LoadTexture("PX05004.bmp");
        _equipmentResearchBackground = LoadTexture("PX05007.bmp");
        _equipmentSellBackground = LoadTexture("PX05013.bmp");
        _equipmentGiveBackground = LoadTexture("PX05015.bmp");
        _movementBackground = LoadTexture("PX05006.bmp");
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
            if (sound is not null) _combatSounds.Add(index, sound);
        }
        foreach (var slot in AudioRouting.GeneralSoundSlots)
        {
            var sound = LoadSound(AudioRouting.GeneralSoundFile(slot));
            if (sound is not null) _generalSounds.Add(slot, sound);
        }
        LoadSoundtrack();
        _diagnostics?.Write("assets.loaded", new Dictionary<string, string?>
        {
            ["helpAvailable"] = (_helpDocument is not null).ToString(),
            ["combatSounds"] = _combatSounds.Count.ToString(),
            ["generalSounds"] = _generalSounds.Count.ToString(),
            ["combatAnimations"] = _combatAnimationTextures.Count.ToString()
        });
    }

    protected override void Update(GameTime gameTime)
    {
        _inputTime = gameTime.TotalGameTime;
        UpdateSoundtrack(gameTime);
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();
        if (UpdatePlanningTimer(gameTime.TotalGameTime))
        {
            _previousKeyboard = keyboard;
            _previousMouse = mouse;
            base.Update(gameTime);
            return;
        }
        RunComputerTurns();
        CaptureNewCombatAnimations();
        foreach (var clip in _combatAnimationPlayer.Advance(gameTime.ElapsedGameTime))
            if (clip.Sound is { } soundIndex) PlayCombatSound(soundIndex);
        if (_combatAnimationPlayer.IsPlaying)
        {
            var cancelPointMapped = VirtualInput.TryMap(
                GraphicsDevice.Viewport, mouse.Position, out var cancelPoint);
            var cancelClicked = cancelPointMapped
                && mouse.LeftButton == ButtonState.Pressed
                && _previousMouse.LeftButton == ButtonState.Released
                && CombatPanelLayout.Cancel.Contains(cancelPoint);
            if (Pressed(keyboard, Keys.Escape) || Pressed(keyboard, Keys.Back)
                || cancelClicked)
            {
                _combatAnimationPlayer.Clear();
                _message = "COMBAT DETAIL SKIPPED";
            }
            else
            {
                _previousKeyboard = keyboard;
                _previousMouse = mouse;
                base.Update(gameTime);
                return;
            }
        }
        if (_screens.Current == ClientScreen.Options)
        {
            UpdateOptions(keyboard);
        }
        else if (_screens.Current == ClientScreen.Help)
        {
            UpdateHelp(keyboard);
        }
        else if (_screens.Current == ClientScreen.ComlinkSend)
        {
            UpdateComlinkSend(keyboard);
        }
        else if (_screens.Current == ClientScreen.Setup && _editingPlayerName is not null)
        {
            UpdateSetupName(keyboard);
        }
        else
        {
            if (!_idleGangWarningOpen)
            {
                if (Pressed(keyboard, Keys.F1)) OpenHelp();
                else if (Pressed(keyboard, Keys.O)) OpenOptions();
                else if (Pressed(keyboard, Keys.Escape) && !_screens.Back()) Exit();
            }
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
                case ClientScreen.ComlinkView:
                    UpdateComlinkView(keyboard);
                    break;
                case ClientScreen.Commands:
                    if (IsMovementCommandPicker())
                    {
                        if (Pressed(keyboard, Keys.Left)) MoveMovementTarget(-1, 0);
                        if (Pressed(keyboard, Keys.Right)) MoveMovementTarget(1, 0);
                        if (Pressed(keyboard, Keys.Up)) MoveMovementTarget(0, -1);
                        if (Pressed(keyboard, Keys.Down)) MoveMovementTarget(0, 1);
                    }
                    else
                    {
                        if (Pressed(keyboard, Keys.Up)) MoveCommandCursor(-1);
                        if (Pressed(keyboard, Keys.Down)) MoveCommandCursor(1);
                    }
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
                        if (Pressed(keyboard, Keys.Left) || Pressed(keyboard, Keys.Up)) CycleGangDetails(-1);
                        if (Pressed(keyboard, Keys.Right) || Pressed(keyboard, Keys.Down)) CycleGangDetails(1);
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
                case ClientScreen.GameInfo:
                    if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
                        _screens.Show(_managementReturnScreen);
                    break;
                case ClientScreen.Finance:
                    if (Pressed(keyboard, Keys.Left)) _financeScope = FinanceScope.City;
                    if (Pressed(keyboard, Keys.Right)) _financeScope = FinanceScope.Sector;
                    if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
                        _screens.Show(_managementReturnScreen);
                    break;
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
                    if (Pressed(keyboard, Keys.V)) OpenGiveEquipment(ClientScreen.Items);
                    if (Pressed(keyboard, Keys.S)) OpenSellEquipment(ClientScreen.Items);
                    if (Pressed(keyboard, Keys.Back)) _screens.Show(ClientScreen.City);
                    break;
                case ClientScreen.Give:
                    if (Pressed(keyboard, Keys.D1)) ToggleGiveSelection(0);
                    if (Pressed(keyboard, Keys.D2)) ToggleGiveSelection(1);
                    if (Pressed(keyboard, Keys.D3)) ToggleGiveSelection(2);
                    if (Pressed(keyboard, Keys.Enter)) OpenGiveTargets();
                    if (Pressed(keyboard, Keys.Back)) CloseGiveEquipment();
                    break;
                case ClientScreen.GiveTarget:
                    if (Pressed(keyboard, Keys.Up)) MoveGiveCursor(-1);
                    if (Pressed(keyboard, Keys.Down)) MoveGiveCursor(1);
                    if (Pressed(keyboard, Keys.Enter)) QueueSelectedGive();
                    if (Pressed(keyboard, Keys.Back)) _screens.Show(ClientScreen.Give);
                    break;
                case ClientScreen.Sell:
                    if (Pressed(keyboard, Keys.D1)) ToggleSellSelection(0);
                    if (Pressed(keyboard, Keys.D2)) ToggleSellSelection(1);
                    if (Pressed(keyboard, Keys.D3)) ToggleSellSelection(2);
                    if (Pressed(keyboard, Keys.Enter)) QueueSelectedSale();
                    if (Pressed(keyboard, Keys.Back)) CloseSellEquipment();
                    break;
                case ClientScreen.CombatSummary:
                    if (Pressed(keyboard, Keys.Left) || Pressed(keyboard, Keys.Up)) MoveCombatSummary(-1);
                    if (Pressed(keyboard, Keys.Right) || Pressed(keyboard, Keys.Down)) MoveCombatSummary(1);
                    if (Pressed(keyboard, Keys.D)) ReplaySelectedCombatDetail();
                    if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
                        _screens.Show(_managementReturnScreen);
                    break;
                case ClientScreen.Search:
                    if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
                        _screens.Show(_managementReturnScreen);
                    break;
            }
        }
        var pointerMapped = VirtualInput.TryMap(GraphicsDevice.Viewport, mouse.Position, out var virtualPoint);
        if (pointerMapped && _slidePanels)
        {
            var offset = _panelSlideTransition.Offset(_screens.Current, gameTime.TotalGameTime);
            virtualPoint = new Point(virtualPoint.X - offset, virtualPoint.Y);
        }
        _hoverPoint = pointerMapped ? virtualPoint : null;
        var wheelDelta = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        if (pointerMapped && _screens.Current == ClientScreen.Help && wheelDelta != 0)
            HandleHelpScroll(virtualPoint, wheelDelta);
        if (pointerMapped && mouse.LeftButton == ButtonState.Pressed)
        {
            _dragPoint = virtualPoint;
            if (_previousMouse.LeftButton == ButtonState.Released) HandleClick(virtualPoint);
            else if (_draggedSetupPlayerSlot is not null && !_setupPlayerDragStarted
                     && (Math.Abs(virtualPoint.X - _setupPlayerPressPoint.X) >= 4
                         || Math.Abs(virtualPoint.Y - _setupPlayerPressPoint.Y) >= 4))
            {
                _setupPlayerDragStarted = true;
                _message = "DROP ON ANOTHER PLAYER COLOR";
            }
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
            && _pressedSetupButton is not null)
        {
            if (pointerMapped) CompleteSetupButton(virtualPoint);
            else _pressedSetupButton = null;
        }
        else if (_previousMouse.LeftButton == ButtonState.Pressed && mouse.LeftButton == ButtonState.Released
                 && _draggedSetupPlayerSlot is not null)
        {
            if (pointerMapped && _setupPlayerDragStarted) CompleteSetupPlayerDrag(virtualPoint);
            else CancelSetupPlayerDrag();
        }
        else if (_previousMouse.LeftButton == ButtonState.Pressed && mouse.LeftButton == ButtonState.Released
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
        var slideOffset = _slidePanels
            ? _panelSlideTransition.Offset(_screens.Current, gameTime.TotalGameTime)
            : 0;
        var transform = Matrix.CreateTranslation(slideOffset, 0, 0)
            * VirtualInput.Transform(viewport);
        _batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: transform);
        _batch.Draw(_pixel, new Rectangle(0, 0, 640, 460), new Color(8, 10, 12));
        switch (_screens.Current)
        {
            case ClientScreen.Title:
                DrawTitle(_batch, _pixel, _font);
                break;
            case ClientScreen.Options:
                DrawOptions(_batch, _pixel, _font);
                break;
            case ClientScreen.Help:
                DrawHelp(_batch, _pixel, _font);
                break;
            case ClientScreen.Setup:
                DrawSetup(_batch, _pixel, _font);
                break;
            case ClientScreen.City when _state is not null:
                DrawBoard(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.GameInfo when _state is not null:
                DrawGameInformation(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Endgame when _state?.Outcome is not null:
                DrawEndgame(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Handoff when _state is not null:
                DrawHandoff(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Events when _state is not null:
                DrawLastTurnEventsPanel(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.ComlinkView when _state is not null:
                DrawComlinkView(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.ComlinkSend when _state is not null:
                DrawComlinkSend(_batch, _pixel, _font, _state);
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
                DrawGiveEquipment(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.GiveTarget when _state is not null:
                DrawGiveTargets(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Sell when _state is not null:
                DrawSellEquipment(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.CombatSummary when _state is not null:
                DrawCombatResultsPanel(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Search when _state is not null:
                DrawSearch(_batch, _pixel, _font, _state);
                break;
        }
        if (_state is not null && _combatAnimationPlayer.IsPlaying)
            DrawCombatPanel(_batch, _pixel, _font, _state);
        DrawPlanningTimer(_batch, _pixel);
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
        if (Pressed(keyboard, Keys.M)) CycleDifficulty();
        if (Pressed(keyboard, Keys.L)) CyclePlanningTimeLimit();
        if (Pressed(keyboard, Keys.Enter)) StartMatch();
    }

    private void HandleClick(Point point)
    {
        switch (_screens.Current)
        {
            case ClientScreen.Title:
                if (TitleNewGame.Contains(point)) _screens.Show(ClientScreen.Setup);
                else if (TitleLoadGame.Contains(point)) LoadQuickGame();
                else if (TitleOptions.Contains(point)) OpenOptions();
                else if (TitleHelp.Contains(point)) OpenHelp();
                else if (TitleQuit.Contains(point)) Exit();
                break;
            case ClientScreen.Options:
                HandleOptionsClick(point);
                break;
            case ClientScreen.Help:
                HandleHelpClick(point);
                break;
            case ClientScreen.Setup:
                var scenario = Array.FindIndex(SetupScenarios, rectangle => rectangle.Contains(point));
                var duration = Array.FindIndex(SetupDurations, rectangle => rectangle.Contains(point));
                var planningTimeLimit = Array.FindIndex(
                    PlanningTimerLayout.SetupChoices.ToArray(),
                    rectangle => rectangle.Contains(point));
                var setupButton = SetupButtonLayout.HitTest(point);
                var playerName = _localSetupRoster.HumanSlots
                    .FirstOrDefault(index => PlayerPortraitLayout.Name(index).Contains(point), -1);
                var previousPortrait = _localSetupRoster.HumanSlots
                    .FirstOrDefault(index => PlayerPortraitLayout.Previous(index).Contains(point), -1);
                var nextPortrait = _localSetupRoster.HumanSlots
                    .FirstOrDefault(index => PlayerPortraitLayout.Next(index).Contains(point), -1);
                var draggedPlayer = _localSetupRoster.HumanSlots
                    .FirstOrDefault(index => PlayerPortraitLayout.SetupLarge(index).Contains(point), -1);
                if (_editingPlayerName is not null
                    && (setupButton is not null || playerName != _editingPlayerName))
                    FinishSetupNameEdit(cancel: false);
                if (setupButton is { } button)
                    BeginSetupButton(button);
                else if (playerName >= 0)
                    BeginSetupNameEdit(playerName);
                else if (scenario >= 0)
                {
                    if (_selectedScenario != (ScenarioId)scenario)
                        PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
                    _selectedScenario = (ScenarioId)scenario;
                }
                else if (duration >= 0)
                {
                    if (_selectedDuration != Durations[duration])
                        PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
                    _selectedDuration = Durations[duration];
                }
                else if (planningTimeLimit >= 0)
                    SelectPlanningTimeLimit((PlanningTimeLimit)planningTimeLimit);
                else if (previousPortrait >= 0) CyclePortrait(previousPortrait, -1);
                else if (nextPortrait >= 0) CyclePortrait(nextPortrait, 1);
                else if (draggedPlayer >= 0) BeginSetupPlayerDrag(draggedPlayer, point);
                else
                {
                    var mentality = Array.FindIndex(SetupAiMentalities, rectangle => rectangle.Contains(point));
                    if (mentality >= 0) SelectDifficulty((AiDifficulty)mentality);
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
            case ClientScreen.ComlinkView:
                HandleComlinkViewClick(point);
                break;
            case ClientScreen.ComlinkSend:
                HandleComlinkSendClick(point);
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
            case ClientScreen.GameInfo:
                if (GameInformationLayout.Ok.Contains(point))
                    _screens.Show(_managementReturnScreen);
                break;
            case ClientScreen.Finance:
                if (CityFinanceCity.Contains(point)) _financeScope = FinanceScope.City;
                else if (CityFinanceSector.Contains(point)) _financeScope = FinanceScope.Sector;
                else if (FinanceLayout.Ok.Contains(point))
                    _screens.Show(_managementReturnScreen);
                break;
            case ClientScreen.Ranking:
                if (PlayerRankingLayout.Ok.Contains(point))
                    _screens.Show(_managementReturnScreen);
                break;
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
                HandleGiveEquipmentClick(point);
                break;
            case ClientScreen.GiveTarget:
                HandleGiveClick(point);
                break;
            case ClientScreen.Sell:
                HandleSellClick(point);
                break;
        }
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

    private static bool IsVisibleCombatEvent(MatchState state, PlayerId viewer, GameEvent gameEvent)
    {
        if (gameEvent.Kind == GameEventKind.PoliceAttackResolved) return gameEvent.Player == viewer;
        if (gameEvent.Action != GangAction.Attack || gameEvent.Resolution is null) return false;
        if (gameEvent.Player == viewer) return true;
        return gameEvent.Target.Kind == CommandTargetKind.Gang
            && state.FindGang(new GangId(gameEvent.Target.Id))?.Owner == viewer;
    }

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

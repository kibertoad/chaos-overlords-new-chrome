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
    /// <summary>
    /// The translucent green a legal drag target is outlined with. Every <see cref="SpriteBatch"/>
    /// here begins with the default premultiplied <see cref="BlendState.AlphaBlend"/>, so the
    /// channels are premultiplied here rather than passed straight through.
    /// </summary>
    private static readonly Color GangDragSectorHighlight =
        Color.FromNonPremultiplied(74, 156, 92, 160);
    /// <summary>
    /// The translucent wash and inner outline a legal minimap destination is painted with. Every
    /// minimap cell already carries a 1-pixel green frame, so an outline on that frame alone is
    /// lost in it; the wash tints the whole tile and the outline sits just inside the frame.
    /// </summary>
    private static readonly Color GangDragDestinationWash =
        Color.FromNonPremultiplied(120, 255, 140, 60);
    private static readonly Color GangDragDestinationOutline =
        Color.FromNonPremultiplied(150, 255, 165, 210);
    private static readonly GameDuration[] Durations = Enum.GetValues<GameDuration>();
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
    private readonly string _saveDirectory;
    private readonly string _autoSavePath;
    private readonly string _replayPath;
    private readonly string _preferencesPath;
    private readonly string _multiplayerRecoveryPath;
    private readonly bool _debugPhaseStepping;
    private readonly RuntimeDiagnostics? _diagnostics;
    private readonly RollingAutoSave _autoSave;
    private SpriteBatch? _batch;
    private Texture2D? _pixel;
    private Texture2D? _titleBackground;
    private Texture2D? _setupBackground;
    private Texture2D? _setupControls;
    private Texture2D? _setupKeyedControls;
    private Texture2D? _cityBackground;
    private readonly Texture2D?[] _cityOwnershipLayers = new Texture2D?[MatchLimits.PlayerCount + 1];
    private Texture2D? _gameInfoBackground;
    private Texture2D? _idleGangWarningBackground;
    private Texture2D? _cityFinanceBackground;
    private Texture2D? _sectorFinanceBackground;
    private Texture2D? _rankingBackground;
    private Texture2D? _gangInfoBackground;
    private Texture2D? _sectorGangsBackground;
    private Texture2D? _gangDefinitionInfoBackground;
    private Texture2D? _siteInfoBackground;
    private Texture2D? _itemInfoBackground;
    private Texture2D? _combatBackground;
    private Texture2D? _combatResultsBackground;
    private Texture2D? _lastTurnEventsBackground;
    private Texture2D? _eventSiteDitherOverlay;
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
    private Texture2D? _siteSearchBackground;
    private Texture2D? _siteMarkerSprites;
    private Texture2D? _sitePortraits;
    private Texture2D? _gangPortraits;
    private Texture2D? _itemPortraits;
    private readonly Texture2D?[] _itemRotationTextures = new Texture2D?[53];
    private Texture2D? _policeSprites;
    private Texture2D? _uiSprites;
    private Texture2D? _uiKeyedSprites;
    private PixelFont? _font;
    private readonly Dictionary<short, SoundEffect> _combatSounds = [];
    private readonly Dictionary<int, SoundEffect> _generalSounds = [];
    private SoundEffectInstance? _activeEffectVoice;
    private readonly Dictionary<string, Texture2D> _combatAnimationTextures = [];
    private readonly CombatAnimationPlayer _combatAnimationPlayer = new();
    private readonly PanelSlideTransition _panelSlideTransition = new();
    private MatchState? _state;
    /// <summary>
    /// Where a player's mutations go, and the only handle on the match's recorder.
    /// </summary>
    /// <remarks>
    /// There is no separate recorder field on purpose. A call site that reached for one would
    /// mutate a hot-seat match correctly and an online one silently wrongly — applied locally and
    /// never recorded as an order — and the two are indistinguishable at the point of the call.
    /// </remarks>
    private MatchActions? _actions;
    private OriginalData? _definitions;
    private readonly ScreenRouter _screens = new();
    private readonly CitySectorClickTracker _citySectorClicks = new();
    private readonly IndexedDoubleClickTracker _sectorGangClicks = new();
    private readonly IndexedDoubleClickTracker _sectorSiteClicks = new();
    private readonly IndexedDoubleClickTracker _influenceSiteClicks = new();
    private readonly IndexedDoubleClickTracker _equipmentItemClicks = new();
    private readonly IndexedDoubleClickTracker _equipmentPortraitClicks = new();
    private readonly IndexedDoubleClickTracker _gangEquipmentItemClicks = new();
    private readonly IndexedDoubleClickTracker _hirePortraitClicks = new();
    private readonly short[] _playerPortraits = Enumerable.Range(0, MatchLimits.PlayerCount)
        .Select(index => checked((short)index)).ToArray();
    private readonly string[] _playerNames = Enumerable.Range(0, MatchLimits.PlayerCount)
        .Select(LocalSetupPolicy.DefaultPlayerName).ToArray();
    private readonly SetupPlayerNameEditor _setupNameEditor = new();
    private readonly LocalSetupRoster _localSetupRoster = new();
    private int _selectedSetupPlayerSlot;
    private int? _editingPlayerName;
    private string _setupOriginalName = string.Empty;
    private ScenarioId _selectedScenario = SetupScenarioButtons.DefaultScenario;
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
    private long _combatSummaryEventSequence = -1;
    private PlayerId? _combatSummaryOpponent;
    private bool _openEventsAfterCombat;
    private bool _automaticDetailedCombatPresentation;
    private bool _showGameInfoAtPlanningEntry;
    private bool _continuePlanningEntryAfterGameInfo;
    private bool _deferComlinkAlertUntilPlanningVisible;
    private readonly Queue<PlayerId> _pendingHotSeatEliminations = [];
    private readonly HashSet<PlayerId> _presentedHotSeatEliminations = [];
    private PlayerId? _eliminationHandoffPlayer;
    private int _eventCursor;
    private readonly HashSet<int> _eventViewedPages = [];
    private readonly LastTurnEventArchive _lastTurnEventArchive = new();
    private int _siteSearchCursor;
    private readonly SiteSearchSelectionState _siteSearchSelections = new();
    private readonly IndexedDoubleClickTracker _siteSearchClicks = new();
    private FinanceScope _financeScope = FinanceScope.City;
    private int _comlinkCursor;
    private readonly bool[] _comlinkRecipients = new bool[MatchLimits.PlayerCount];
    private readonly ComlinkTextEditor _comlinkEditor = new();
    private readonly ComlinkCaretCadence _comlinkCaretCadence = new();
    private ComlinkSendButton? _pressedComlinkSendButton;
    private readonly ComlinkAlertCadence _comlinkAlertCadence = new();
    private readonly BackgroundRedrawCadence _backgroundRedrawCadence = new();
    private string _comlinkStatus = string.Empty;
    private IReadOnlyList<GameCommand> _giveOptions = [];
    private int _giveCursor;
    private IReadOnlyList<GangId> _sectorGangRoster = [];
    private int _sectorGangCursor;
    private ClientScreen _sectorGangReturnScreen = ClientScreen.City;
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
    private CityConsoleControl? _pressedCityConsoleControl;
    private CityConsoleAction? _pressedCityConsoleAction;
    private ClientScreen _pressedCityConsoleReturnScreen;
    private short? _draggedHireDefinitionId;
    private Point _hirePressPoint;
    private Point _setupPlayerPressPoint;
    private bool _hireDragStarted;
    private bool _setupPlayerDragStarted;
    private GangId? _draggedGangId;
    private Point _gangPressPoint;
    private bool _gangDragStarted;
    private SectorGangDragProjection? _gangDragProjection;
    private Point _dragPoint;
    private string _message = string.Empty;
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;
    private readonly CombatPresentationProgress _combatPresentationProgress = new();
    private TimeSpan _inputTime;
    private ClientScreen _gangDetailsReturnScreen = ClientScreen.City;
    private GangId? _gangDetailsInstanceId;
    private short? _gangDetailsDefinitionId;
    private int? _gangDetailsSectorFilter;
    private ClientScreen _siteDetailsReturnScreen = ClientScreen.Sector;
    private int? _siteDetailsSectorId;
    private int? _siteDetailsSlot;
    private short? _siteDetailsDefinitionId;
    private short? _itemDetailsId;
    private ClientScreen _itemDetailsReturnScreen = ClientScreen.Commands;

    public ChaosGame(
        string assetRoot,
        bool debugPhaseStepping = false,
        RuntimeDiagnostics? diagnostics = null,
        string? screenshotFolder = null)
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
            if (!KeepsGangSelection(current)) _gangSelection.Clear();
            _citySectorClicks.Cancel();
            _sectorSiteClicks.Cancel();
            _sectorGangClicks.Cancel();
            _siteSearchClicks.Cancel();
            if (_slidePanels) _panelSlideTransition.Begin(previous, current, _inputTime);
            foreach (var slot in AudioRouting.PanelTransitionSounds(previous, current, _slidePanels))
                PlayGeneralSound(slot);
            if (current == ClientScreen.Endgame && _state?.Outcome is not null)
            {
                _showEndgameNotice = EndgameNoticePresentation.For(_state) is not null;
                _showEndgameStats = false;
            }
        };
        var userDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Rechaos Overlords");
        _saveDirectory = userDataRoot;
        NativeSaveStore.DeleteStaleTemporaryFiles(userDataRoot);
        ConfigureScreenshotOutput(screenshotFolder, userDataRoot);
        _autoSavePath = Path.Combine(userDataRoot, "autosave.rchsave");
        _autoSave = new RollingAutoSave(_autoSavePath, ReportAutoSaveFailure);
        _replayPath = Path.Combine(userDataRoot, "last-match.rchreplay");
        _preferencesPath = Path.Combine(userDataRoot, "preferences.json");
        _multiplayerRecoveryPath = Path.Combine(userDataRoot, "multiplayer-recovery.json");
        var preferences = GamePreferencesStore.LoadOrDefault(_preferencesPath);
        _musicVolumeLevel = preferences.MusicVolumeLevel;
        _soundEffectVolumeLevel = preferences.SoundEffectVolumeLevel;
        _warnIfIdleGangs = preferences.WarnIfIdleGangs;
        _selectedPlanningTimeLimit = preferences.PlanningTimeLimit;
        _showBaseStatistics = preferences.ShowBaseStatistics;
        _detailedCombat = preferences.DetailedCombat;
        _slidePanels = preferences.SlidePanels;
        _fullscreen = preferences.Fullscreen;
        _smoothEventSiteImages = preferences.SmoothEventSiteImages;
        _introMoviesSeen = preferences.IntroMoviesSeen;
        _defaultAiPolicy = preferences.DefaultAiPolicy;
        _online.Service = preferences.OnlineService;
        _online.Server.Set(preferences.CustomMultiplayerServer);
        _onlineLobbyPresentation = preferences.LobbyPresentation;
        _multiplayerRecoveries.AddRange(MultiplayerRecoveryStore.LoadAll(_multiplayerRecoveryPath));
        if (LatestOnlineRecovery is { } recovery)
        {
            _online.JoinCode.Set(recovery.JoinCode);
            _online.DisplayName.Set(recovery.DisplayName);
            if (recovery.ShouldSuggestReconnect)
                _message = "ONLINE MATCH INTERRUPTED  OPEN ONLINE TO RECONNECT";
        }
        // Two virtual pixels per device pixel is the size the interface was drawn for, but a
        // 1366x768 laptop or a 1080p panel at 150% scaling cannot show a 920-pixel-tall window, and
        // the DONE button ends up below the screen edge. Draw and input already letterbox from the
        // viewport, so any size works; the window just has to fit on the display it opens on.
        var (backBufferWidth, backBufferHeight) = PreferredBackBufferSize();
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = backBufferWidth,
            PreferredBackBufferHeight = backBufferHeight,
            SynchronizeWithVerticalRetrace = true,
            HardwareModeSwitch = false,
            IsFullScreen = _fullscreen
        };
        // Ticking on without focus is what keeps background online notices, the planning timer and
        // the autosave serviced; the redraw is the expensive part, and BeginDraw spaces that out
        // instead. The sleep between inactive ticks doubles as the input poll gap, because every
        // edge comes from comparing consecutive polled snapshots, so it stays far shorter than a
        // click: a press and release that both landed inside one sleep would never be seen, and
        // the click that raises the window would be swallowed.
        InactiveSleepTime = TimeSpan.FromMilliseconds(20);
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "Chaos Overlords: New Chrome";
        // The only text the game takes: a server address, a name and a join code. The platform has
        // already decoded the keystroke, so a non-US layout types what it should.
        Window.TextInput += (_, args) => HandleTextInput(args.Character);
    }

    /// <summary>
    /// The largest whole multiple of the 640x460 interface that fits the display, at least 1x.
    /// </summary>
    /// <remarks>
    /// Whole multiples keep the pixel art on exact pixel boundaries. The usable area is taken as
    /// nine tenths of the display so the window is not flush against the taskbar and the title bar.
    /// </remarks>
    private static (int Width, int Height) PreferredBackBufferSize()
    {
        const int preferredScale = 2;
        var display = GraphicsAdapter.DefaultAdapter?.CurrentDisplayMode;
        if (display is null) return (VirtualInput.Width * preferredScale, VirtualInput.Height * preferredScale);
        var scale = Math.Min(
            display.Width * 9 / 10 / VirtualInput.Width,
            display.Height * 9 / 10 / VirtualInput.Height);
        scale = Math.Clamp(scale, 1, preferredScale);
        return (VirtualInput.Width * scale, VirtualInput.Height * scale);
    }

    protected override void LoadContent()
    {
        ValidateAssetPack();
        _batch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
        _eventSiteDitherOverlay = LastTurnEventPresentation.CreateEventSiteDitherOverlay(
            GraphicsDevice);
        _definitions = BundledOriginalData.Load();
        _helpDocument = ExtractedHelpStore.LoadOrNull(_assetRoot) is { } help
            ? HelpContentAugmentation.AddExecutableNotes(help)
            : null;

        _titleBackground = LoadTexture("PX00130.bmp");
        _setupBackground = LoadTexture("PX00143.bmp");
        _setupControls = LoadTexture("PX00140.bmp");
        _setupKeyedControls = LoadTexture("PX00140.bmp", transparentWhite: true);
        _cityBackground = LoadTexture("PX00128.bmp");
        for (var index = 0; index < _cityOwnershipLayers.Length; index++)
            _cityOwnershipLayers[index] = LoadTexture($"PX1000{index}.bmp");
        _endgameBackground = LoadTexture("PX00200.bmp");
        _endgameSprites = LoadTexture("PX00201.bmp");
        _victoryBackground = LoadTexture("PX00202.bmp");
        _eliminationBackground = LoadTexture("PX00203.bmp");
        _gameInfoBackground = LoadTexture("PX05021.bmp");
        _idleGangWarningBackground = LoadTexture("PX05020.bmp");
        _cityFinanceBackground = LoadTexture("PX05008.bmp");
        _sectorFinanceBackground = LoadTexture("PX05019.bmp");
        _rankingBackground = LoadTexture("PX05011.bmp");
        _handoffPanel = LoadTexture("PX00132.bmp");
        _gangInfoBackground = LoadTexture("PX05000.bmp");
        _sectorGangsBackground = LoadTexture("PX05009.bmp");
        _gangDefinitionInfoBackground = LoadTexture("PX05022.bmp");
        _siteInfoBackground = LoadTexture("PX05002.bmp");
        _itemInfoBackground = LoadTexture("PX05001.bmp");
        _combatBackground = LoadTexture("PX05014.bmp");
        _combatResultsBackground = LoadTexture("PX05012.bmp");
        _lastTurnEventsBackground = LoadTexture("PX05010.bmp");
        _comlinkViewBackground = LoadTexture("PX05017.bmp");
        _comlinkSendBackground = LoadTexture("PX05018.bmp");
        for (var eventArt = 1; eventArt <= 9; eventArt++)
            _lastTurnEventArtwork[eventArt] = LoadTexture(
                $"PX060{eventArt:00}.bmp", transparentWhite: eventArt == 4);
        _hireComparisonBackground = LoadTexture("PX05016.bmp");
        _influenceBackground = LoadTexture("PX05005.bmp");
        _targetAcquisitionBackground = LoadTexture("PX05003.bmp");
        _equipmentPurchaseBackground = LoadTexture("PX05004.bmp");
        _equipmentResearchBackground = LoadTexture("PX05007.bmp");
        _equipmentSellBackground = LoadTexture("PX05013.bmp");
        _equipmentGiveBackground = LoadTexture("PX05015.bmp");
        _movementBackground = LoadTexture("PX05006.bmp");
        _siteSearchBackground = LoadTexture("PX05024.bmp");
        _siteMarkerSprites = LoadTexture("PX00150.bmp", transparentWhite: true);
        _sitePortraits = LoadTexture("PX02000.bmp");
        _gangPortraits = LoadTexture("PX03000.bmp");
        _itemPortraits = LoadTexture("PX04999.bmp");
        for (var itemId = 0; itemId < _itemRotationTextures.Length; itemId++)
            _itemRotationTextures[itemId] = LoadTexture($"PX04{itemId:000}.bmp");
        _policeSprites = LoadTexture("PX00300.bmp");
        _uiSprites = LoadTexture("PX00129.bmp");
        _uiKeyedSprites = LoadTexture("PX00129.bmp", transparentWhite: true);
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
        InitializeIntroMovies();
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
        _autoSave.Pump();
        _inputTime = gameTime.TotalGameTime;
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();
        _multiSelectModifier = keyboard.IsKeyDown(Keys.LeftControl)
            || keyboard.IsKeyDown(Keys.RightControl);
        if (Pressed(keyboard, Keys.F12)) _screenshotRequested = true;
        var altEnter = Pressed(keyboard, Keys.Enter)
            && (keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt));
        if (Pressed(keyboard, Keys.F11) || altEnter) ToggleFullscreen();
        if (altEnter || UpdateIntroMovies(gameTime, keyboard, mouse))
        {
            EndUpdate(gameTime, keyboard, mouse);
            return;
        }
        UpdateSoundtrack(gameTime);
        UpdateComlinkAlert(gameTime.TotalGameTime);
        UpdateComlinkCaret(gameTime.TotalGameTime);
        PumpBugReportSend();
        // Before the planning timer, so a turn that resolved on the server is adopted even on the
        // frame the local clock would otherwise have taken over the loop.
        UpdateOnlineSession();
        var rightClicked = PointerButtonEdges.Pressed(
            mouse.RightButton, _previousMouse.RightButton);
        if (!_gameMenuOpen && UpdatePlanningTimer(gameTime.TotalGameTime))
        {
            EndUpdate(gameTime, keyboard, mouse);
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
                || cancelClicked || rightClicked)
            {
                // The cue belongs to the clip being skipped, and the original unloads slot 5 once
                // a combatant's sequence ends, so it does not outlive the presentation.
                _combatAnimationPlayer.Clear();
                StopEffectVoice();
                _message = string.Empty;
                rightClicked = false;
            }
            else
            {
                EndUpdate(gameTime, keyboard, mouse);
                return;
            }
        }
        if (_automaticDetailedCombatPresentation)
            FinishAutomaticCombatPresentation();
        if (rightClicked && !_gameMenuOpen) CancelCurrentInteraction();
        if (_gameMenuOpen)
        {
            UpdateGameMenu(keyboard);
        }
        else if (_screens.Current == ClientScreen.Options)
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
            // A printable key belongs exclusively to the field showing the caret. In particular,
            // typing an O in an online name or join code must not open Options before the window's
            // TextInput event can deliver that character to the field.
            if (!_idleGangWarningOpen && !TextInputHasFocus())
            {
                if (Pressed(keyboard, Keys.F1)) OpenHelp();
                else if (Pressed(keyboard, Keys.O)) OpenOptions();
                else if (Pressed(keyboard, Keys.Escape))
                {
                    if (_configuringOnlineLobby) CloseOnlineSetup();
                    else if (_state is not null && _screens.Current is not ClientScreen.Title)
                        OpenGameMenu();
                    else if (!_screens.Back()) Exit();
                }
            }
            if (!_gameMenuOpen && !TakeoverVoteBlocksInput) switch (_screens.Current)
            {
                case ClientScreen.Title:
                    UpdateTitle(keyboard);
                    break;
                case ClientScreen.Setup:
                    UpdateSetup(keyboard);
                    break;
                case ClientScreen.Online:
                    UpdateOnline(keyboard);
                    break;
                case ClientScreen.Lobby:
                    UpdateLobby(keyboard, gameTime);
                    break;
                case ClientScreen.City:
                    UpdateCity(keyboard);
                    break;
                case ClientScreen.Endgame:
                    if (!_showEndgameNotice && Pressed(keyboard, Keys.A)) _showEndgameStats = false;
                    if (!_showEndgameNotice && Pressed(keyboard, Keys.S)) _showEndgameStats = true;
                    if (Pressed(keyboard, Keys.Enter)) AdvanceEndgamePresentation();
                    break;
                case ClientScreen.Handoff:
                    if (Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Space))
                        FinishHandoff();
                    break;
                case ClientScreen.Elimination:
                    if (Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Space))
                        FinishHotSeatEliminationPresentation();
                    break;
                case ClientScreen.Events:
                    if (Pressed(keyboard, Keys.Left) || Pressed(keyboard, Keys.Up)) MoveEventCursor(-1);
                    if (Pressed(keyboard, Keys.Right) || Pressed(keyboard, Keys.Down)) MoveEventCursor(1);
                    if (Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Delete)
                        || Pressed(keyboard, Keys.Back))
                        AcceptAndInvoke(CloseEvents);
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
                    if (Pressed(keyboard, Keys.Back))
                        AcceptAndInvoke(BackFromCommands);
                    break;
                case ClientScreen.Hire:
                    if (Pressed(keyboard, Keys.Left) || Pressed(keyboard, Keys.Up)) MoveHireCursor(-1);
                    if (Pressed(keyboard, Keys.Right) || Pressed(keyboard, Keys.Down)) MoveHireCursor(1);
                    if (Pressed(keyboard, Keys.S)) SnubSelectedHireOffer();
                    if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
                        AcceptAndShow(_managementReturnScreen);
                    break;
                case ClientScreen.Sector:
                    UpdateSector(keyboard);
                    break;
                case ClientScreen.SectorGangs:
                    if (Pressed(keyboard, Keys.Left) || Pressed(keyboard, Keys.Up))
                        MoveSectorGangCursor(-1);
                    if (Pressed(keyboard, Keys.Right) || Pressed(keyboard, Keys.Down))
                        MoveSectorGangCursor(1);
                    if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
                        AcceptAndInvoke(CloseSectorGangs);
                    break;
                case ClientScreen.Gang:
                    if (_gangDetailsInstanceId is not null)
                    {
                        if (Pressed(keyboard, Keys.Left) || Pressed(keyboard, Keys.Up)) CycleGangDetails(-1);
                        if (Pressed(keyboard, Keys.Right) || Pressed(keyboard, Keys.Down)) CycleGangDetails(1);
                    }
                    if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
                        AcceptAndInvoke(CloseGangDetails);
                    break;
                case ClientScreen.Site:
                    if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
                        AcceptAndInvoke(CloseSiteDetails);
                    break;
                case ClientScreen.ItemInformation:
                    if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
                        AcceptAndInvoke(CloseItemDetails);
                    break;
                case ClientScreen.GameInfo:
                    if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
                        AcceptAndInvoke(CloseGameInformation);
                    break;
                case ClientScreen.Finance:
                case ClientScreen.Ranking:
                    if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
                        AcceptAndShow(_managementReturnScreen);
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
                    if (Pressed(keyboard, Keys.Up)) MoveGiveCursor(-1);
                    if (Pressed(keyboard, Keys.Down)) MoveGiveCursor(1);
                    if (Pressed(keyboard, Keys.Enter)) QueueSelectedGive();
                    if (Pressed(keyboard, Keys.Back))
                        AcceptAndInvoke(CloseGiveEquipment);
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
                    if (Pressed(keyboard, Keys.Back))
                        AcceptAndInvoke(CloseSellEquipment);
                    break;
                case ClientScreen.CombatSummary:
                    if (Pressed(keyboard, Keys.Left) || Pressed(keyboard, Keys.Up)) MoveCombatSummary(-1);
                    if (Pressed(keyboard, Keys.Right) || Pressed(keyboard, Keys.Down)) MoveCombatSummary(1);
                    if (Pressed(keyboard, Keys.D)) ReplaySelectedCombatDetail();
                    if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
                        AcceptAndInvoke(CloseCombatResults);
                    break;
                case ClientScreen.Search:
                    if (Pressed(keyboard, Keys.Left))
                        MoveSiteSearchCursor(-SiteSearchLayout.RowsPerColumn);
                    if (Pressed(keyboard, Keys.Right))
                        MoveSiteSearchCursor(SiteSearchLayout.RowsPerColumn);
                    if (Pressed(keyboard, Keys.Up)) MoveSiteSearchCursor(-1);
                    if (Pressed(keyboard, Keys.Down)) MoveSiteSearchCursor(1);
                    if (Pressed(keyboard, Keys.Space)) ToggleSiteSearchSelection();
                    if (Pressed(keyboard, Keys.A)) SelectAllSiteSearch();
                    if (Pressed(keyboard, Keys.N)) ClearSiteSearch();
                    if (Pressed(keyboard, Keys.Enter)) ApplySiteSearch();
                    if (Pressed(keyboard, Keys.Back)) CancelSiteSearch();
                    break;
            }
        }
        var pointerMapped = VirtualInput.TryMap(GraphicsDevice.Viewport, mouse.Position, out var virtualPoint);
        if (pointerMapped && _slidePanels && !_gameMenuOpen)
        {
            var offset = _panelSlideTransition.Offset(_screens.Current, gameTime.TotalGameTime);
            virtualPoint = new Point(virtualPoint.X - offset, virtualPoint.Y);
        }
        UpdateHoverPoint(pointerMapped ? virtualPoint : null);
        var wheelDelta = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        if (pointerMapped && _screens.Current == ClientScreen.Help && wheelDelta != 0)
            HandleHelpScroll(virtualPoint, wheelDelta);
        if (pointerMapped && mouse.LeftButton == ButtonState.Pressed)
        {
            _dragPoint = virtualPoint;
            if (_previousMouse.LeftButton == ButtonState.Released) HandleClick(virtualPoint);
            else if (_draggedSetupPlayerSlot is not null && !_setupPlayerDragStarted
                     && PlayerPortraitLayout.SetupDragMoved(
                         _setupPlayerPressPoint, virtualPoint))
            {
                _setupPlayerDragStarted = true;
                _message = string.Empty;
            }
            else if (_draggedHireDefinitionId is not null && !_hireDragStarted
                     && DragMoved(_hirePressPoint, virtualPoint))
            {
                _hireDragStarted = true;
                _message = string.Empty;
            }
            else if (_draggedGangId is not null && !_gangDragStarted
                     && DragMoved(_gangPressPoint, virtualPoint))
            {
                StartGangDrag();
                _message = string.Empty;
            }
        }
        if (_previousMouse.LeftButton == ButtonState.Pressed && mouse.LeftButton == ButtonState.Released)
            CompletePointerRelease(pointerMapped, virtualPoint);
        CaptureNewCombatAnimations();
        EndUpdate(gameTime, keyboard, mouse);
    }

    /// <summary>Records this frame's input as the previous frame's, which every edge test reads.</summary>
    private void EndUpdate(GameTime gameTime, KeyboardState keyboard, MouseState mouse)
    {
        _previousKeyboard = keyboard;
        _previousMouse = mouse;
        base.Update(gameTime);
    }

    private static bool DragMoved(Point press, Point current) =>
        Math.Abs(current.X - press.X) >= 4 || Math.Abs(current.Y - press.Y) >= 4;

    private void HandleClick(Point point)
    {
        if (HandleOnlineErrorPopupClick(point) || HandleReconnectPopupClick(point)) return;
        if (_gameMenuOpen)
        {
            HandleGameMenuClick(point);
            return;
        }
        if (HandleTakeoverVoteClick(point)) return;
        switch (_screens.Current)
        {
            case ClientScreen.Title:
                if (TitleNewGame.Contains(point)) OpenNewGameSetup();
                else if (TitleLoadGame.Contains(point)) OpenSaveBrowser(saving: false, fromTitle: true);
                else if (TitleOnline.Contains(point)) OpenOnline();
                else if (TitleOptions.Contains(point)) OpenOptions();
                else if (TitleHelp.Contains(point)) OpenHelp();
                else if (TitleIntro.Contains(point)) ReplayIntroMovies();
                else if (TitleQuit.Contains(point)) Exit();
                break;
            case ClientScreen.Options:
                HandleOptionsClick(point);
                break;
            case ClientScreen.Help:
                HandleHelpClick(point);
                break;
            case ClientScreen.Online:
                HandleOnlineClick(point);
                break;
            case ClientScreen.Lobby:
                HandleLobbyClick(point);
                break;
            case ClientScreen.Setup:
                var scenario = Array.FindIndex(SetupScenarios, rectangle => rectangle.Contains(point));
                var duration = Array.FindIndex(SetupDurations, rectangle => rectangle.Contains(point));
                var planningTimeLimit = Array.FindIndex(
                    PlanningTimerLayout.SetupChoices.ToArray(),
                    rectangle => rectangle.Contains(point));
                var setupButton = SetupButtonLayout.HitTest(point);
                var playerName = (_configuringOnlineLobby ? [] : _localSetupRoster.HumanSlots)
                    .FirstOrDefault(index => PlayerPortraitLayout.NameHit(index).Contains(point), -1);
                var setupPlayer = (_configuringOnlineLobby ? [] : _localSetupRoster.HumanSlots)
                    .FirstOrDefault(index => PlayerPortraitLayout.SetupHit(index).Contains(point), -1);
                if (_editingPlayerName is not null
                    && (setupButton is not null || playerName != _editingPlayerName))
                    FinishSetupNameEdit(cancel: false);
                if (setupButton is { } button)
                    BeginSetupButton(button);
                else if (scenario >= 0)
                    SelectSetupScenarioButton(scenario);
                else if (duration >= 0)
                    SelectSetupDurationButton(duration);
                else if (planningTimeLimit >= 0)
                    SelectPlanningTimeLimit((PlanningTimeLimit)planningTimeLimit);
                else if (setupPlayer >= 0) BeginSetupPlayerDrag(setupPlayer, point);
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
                HandleEndgameClick(point);
                break;
            case ClientScreen.Handoff:
                HandleHandoffClick(point);
                break;
            case ClientScreen.Elimination:
                HandleHotSeatEliminationClick(point);
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
            case ClientScreen.SectorGangs:
                if (SectorGangsLayout.Ok.Contains(point))
                    AcceptAndInvoke(CloseSectorGangs);
                break;
            case ClientScreen.Gang:
            {
                if (HandleGangDetailsEquipmentClick(point)) break;
                var gangOk = _gangDetailsInstanceId is null
                    ? GangDefinitionInformationLayout.Ok
                    : GangInformationLayout.Ok;
                if (gangOk.Contains(point))
                    AcceptAndInvoke(CloseGangDetails);
                break;
            }
            case ClientScreen.Site:
                if (SiteInformationLayout.Ok.Contains(point))
                    AcceptAndInvoke(CloseSiteDetails);
                break;
            case ClientScreen.ItemInformation:
                if (ItemInformationLayout.Ok.Contains(point))
                    AcceptAndInvoke(CloseItemDetails);
                break;
            case ClientScreen.GameInfo:
                if (GameInformationLayout.Ok.Contains(point))
                    AcceptAndInvoke(CloseGameInformation);
                break;
            case ClientScreen.Finance:
                if (FinanceLayout.Ok.Contains(point))
                    AcceptAndShow(_managementReturnScreen);
                break;
            case ClientScreen.Ranking:
                if (PlayerRankingLayout.Ok.Contains(point))
                    AcceptAndShow(_managementReturnScreen);
                break;
            case ClientScreen.Search:
                HandleSiteSearchClick(point);
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
        _message = string.Empty;
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
            && state.FindCombatant(gameEvent, new GangId(gameEvent.Target.Id))?.Owner == viewer;
    }

    private bool Pressed(KeyboardState current, Keys key) => current.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

}

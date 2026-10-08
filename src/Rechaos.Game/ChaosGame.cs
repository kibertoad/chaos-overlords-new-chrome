using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Http;

namespace Rechaos.Game;

public sealed partial class ChaosGame
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
    /// The translucent wash and inner outline a legal destination on the nine-sector display is
    /// painted with. Every cell of the display is cropped from the city map, whose art draws a
    /// 1-pixel green grid edge round it, so an outline on that edge alone is lost in it; the wash
    /// tints the whole tile and the outline sits just inside the edge.
    /// </summary>
    private static readonly Color GangDragDestinationWash =
        Color.FromNonPremultiplied(120, 255, 140, 60);
    private static readonly Color GangDragDestinationOutline =
        Color.FromNonPremultiplied(150, 255, 165, 210);
    private static readonly GameDuration[] Durations = Enum.GetValues<GameDuration>();
    private static readonly Rectangle ManagementBack = new(322, 414, 96, 28);
    /// <summary>The program the game runs in; see <see cref="IGameShell"/>.</summary>
    private IGameShell _shell = DetachedShell.Instance;
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
    private Texture2D? TitleBackground => Texture(OriginalBitmap.TitleBackground);
    private Texture2D? SetupBackground => Texture(OriginalBitmap.SetupBackground);
    private Texture2D? SetupControls => Texture(OriginalBitmap.SetupControls);
    private Texture2D? SetupKeyedControls => Texture(OriginalBitmap.SetupKeyedControls);
    private Texture2D? CityBackground => Texture(OriginalBitmap.CityBackground);
    private Texture2D? GameInfoBackground => Texture(OriginalBitmap.GameInfoBackground);
    private Texture2D? IdleGangWarningBackground => Texture(OriginalBitmap.IdleGangWarningBackground);
    private Texture2D? CityFinanceBackground => Texture(OriginalBitmap.CityFinanceBackground);
    private Texture2D? SectorFinanceBackground => Texture(OriginalBitmap.SectorFinanceBackground);
    private Texture2D? RankingBackground => Texture(OriginalBitmap.RankingBackground);
    private Texture2D? GangInfoBackground => Texture(OriginalBitmap.GangInfoBackground);
    private Texture2D? SectorGangsBackground => Texture(OriginalBitmap.SectorGangsBackground);
    private Texture2D? GangDefinitionInfoBackground => Texture(OriginalBitmap.GangDefinitionInfoBackground);
    private Texture2D? SiteInfoBackground => Texture(OriginalBitmap.SiteInfoBackground);
    private Texture2D? ItemInfoBackground => Texture(OriginalBitmap.ItemInfoBackground);
    private Texture2D? CombatBackground => Texture(OriginalBitmap.CombatBackground);
    private Texture2D? CombatResultsBackground => Texture(OriginalBitmap.CombatResultsBackground);
    private Texture2D? LastTurnEventsBackground => Texture(OriginalBitmap.LastTurnEventsBackground);
    private Texture2D? _eventSiteDitherOverlay;
    private Texture2D? ComlinkViewBackground => Texture(OriginalBitmap.ComlinkViewBackground);
    private Texture2D? ComlinkSendBackground => Texture(OriginalBitmap.ComlinkSendBackground);
    private Texture2D? HireComparisonBackground => Texture(OriginalBitmap.HireComparisonBackground);
    private Texture2D? InfluenceBackground => Texture(OriginalBitmap.InfluenceBackground);
    private Texture2D? TargetAcquisitionBackground => Texture(OriginalBitmap.TargetAcquisitionBackground);
    private Texture2D? EquipmentPurchaseBackground => Texture(OriginalBitmap.EquipmentPurchaseBackground);
    private Texture2D? EquipmentResearchBackground => Texture(OriginalBitmap.EquipmentResearchBackground);
    private Texture2D? EquipmentSellBackground => Texture(OriginalBitmap.EquipmentSellBackground);
    private Texture2D? EquipmentGiveBackground => Texture(OriginalBitmap.EquipmentGiveBackground);
    private Texture2D? MovementBackground => Texture(OriginalBitmap.MovementBackground);
    private Texture2D? SiteSearchBackground => Texture(OriginalBitmap.SiteSearchBackground);
    private Texture2D? SiteMarkerSprites => Texture(OriginalBitmap.SiteMarkerSprites);
    private Texture2D? SitePortraits => Texture(OriginalBitmap.SitePortraits);
    private Texture2D? GangPortraits => Texture(OriginalBitmap.GangPortraits);
    private Texture2D? ItemPortraits => Texture(OriginalBitmap.ItemPortraits);
    private Texture2D? PoliceSprites => Texture(OriginalBitmap.PoliceSprites);
    private Texture2D? UiSprites => Texture(OriginalBitmap.UiSprites);
    private Texture2D? UiKeyedSprites => Texture(OriginalBitmap.UiKeyedSprites);
    private PixelFont? _font;
    /// <summary>The effects the asset pack loads into, unless a test plays them elsewhere.</summary>
    private readonly NativeSoundEffects? _nativeSoundEffects;
    private readonly ISoundEffectOutput _soundEffects;
    private readonly CombatAnimationPlayer _combatAnimationPlayer = new();
    private readonly DetailedCombatExit _combatExit = new();
    private readonly PanelSlideTransition _panelSlideTransition = new();
    private readonly GangSightSnapshotCache _gangSight = new();
    private readonly GangStatusMarkerMap _gangMarkers = new();
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
    private readonly IndexedDoubleClickTracker _sectorNeighborClicks = new();
    private readonly PointDoubleClickTracker _sectorRightClicks = new();
    private readonly IndexedDoubleClickTracker _influenceSiteClicks = new();
    private readonly IndexedDoubleClickTracker _equipmentItemClicks = new();
    private readonly IndexedDoubleClickTracker _equipmentPortraitClicks = new();
    private readonly IndexedDoubleClickTracker _attackTargetClicks = new();
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
    private bool _setupNameSelecting;
    private TimeSpan _setupNameCaretShownAt;
    private ScenarioId _selectedScenario = ScenarioId.Greed;
    private GameDuration _selectedDuration = GameDuration.OneYear;
    // RULE-SETUP-002: the scenario a fresh local setup selects, Greed when nothing is stored.
    private ScenarioId _preferredScenario = ScenarioId.Greed;
    private bool _scenarioPreferenceUnsaved;
    // RULE-SETUP-010: the roster of the last Begin of this session.
    private LocalSetupSnapshot? _begunLocalSetup;
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
    private int _combatSummarySector = -1;
    private GangId? _combatSummaryFocal;
    private GangId? _combatSummaryFocalTarget;
    private PlayerId? _combatSummaryOpponent;
    private bool _openEventsAfterCombat;
    private bool _automaticDetailedCombatPresentation;
    // RULE-SETUP-008: the turn a loaded match resumed on; each local human who plans in it sees
    // Game Information once, after the Ready card.
    private int? _resumedMatchTurn;
    private readonly HashSet<PlayerId> _resumedGameInfoShown = [];
    private bool _continuePlanningEntryAfterGameInfo;
    private bool _deferComlinkAlertUntilPlanningVisible;
    private readonly Queue<PlayerId> _pendingHotSeatEliminations = [];
    private readonly HashSet<PlayerId> _presentedHotSeatEliminations = [];
    private PlayerId? _eliminationHandoffPlayer;
    // RULE-OBJECTIVE-005: set after an elimination card with no local human playing after it in
    // slot order, which leaves the endgame music on where the gameplay music would play.
    private bool _eliminationMusicHeld;
    private int _eventCursor;
    private readonly HashSet<int> _eventViewedPages = [];
    private readonly LastTurnEventArchive _lastTurnEventArchive = new();
    private FinanceScope _financeScope = FinanceScope.City;
    private int _comlinkCursor;
    private readonly bool[] _comlinkRecipients = new bool[MatchLimits.PlayerCount];
    private readonly ComlinkTextEditor _comlinkEditor = new();
    private readonly ComlinkCaretCadence _comlinkCaretCadence = new();
    private ComlinkSendButton? _pressedComlinkSendButton;
    private readonly ComlinkAlertCadence _comlinkAlertCadence = new();
    private readonly BackgroundRedrawCadence _backgroundRedrawCadence = new();
    private readonly PresentationPointer _pointer;
    private string _comlinkStatus = string.Empty;
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
    private SetupPanelControl? _pressedSetupPanelControl;
    private CityConsoleControl? _pressedCityConsoleControl;
    private CityConsoleAction? _pressedCityConsoleAction;
    private ClientScreen _pressedCityConsoleReturnScreen;
    private bool _pressedCityConsoleByRightButton;
    private short? _draggedHireDefinitionId;
    private Point _hirePressPoint;
    private Point _setupPlayerPressPoint;
    private bool _hireDragStarted;
    private bool _setupPlayerDragStarted;
    private GangId? _draggedGangId;
    private Point _gangPressPoint;
    private bool _gangDragStarted;
    private SectorGangDragProjection? _gangDragProjection;
    /// <summary>
    /// Set when a cancel lets go of a hold the planning loop does not run through while the left
    /// button is still down, and cleared when it comes up (FND-UI-044, FND-HIRE-008).
    /// </summary>
    private bool _leftHoldOutlivesCancel;
    /// <summary>
    /// The same for a console tile the right button holds, cleared when the right button comes up
    /// (FND-UI-063).
    /// </summary>
    private bool _rightHoldOutlivesCancel;
    private Point _dragPoint;
    private string _message = string.Empty;
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;
    private readonly CombatPresentationProgress _combatPresentationProgress = new();
    private TimeSpan _inputTime;
    private readonly EventPumpClock _eventPump = new();
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
        MatchDeviations localDeviations,
        bool debugPhaseStepping = false,
        RuntimeDiagnostics? diagnostics = null,
        string? screenshotFolder = null,
        ReferenceFrameRequest? referenceFrame = null,
        string? startupSavePath = null)
        : this(assetRoot, ChaosGameServices.Desktop, localDeviations, debugPhaseStepping, diagnostics,
            screenshotFolder, referenceFrame, startupSavePath)
    {
    }

    internal ChaosGame(
        string assetRoot,
        ChaosGameServices services,
        MatchDeviations localDeviations,
        bool debugPhaseStepping = false,
        RuntimeDiagnostics? diagnostics = null,
        string? screenshotFolder = null,
        ReferenceFrameRequest? referenceFrame = null,
        string? startupSavePath = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        _assetRoot = assetRoot;
        if (services.SoundEffects is { } soundEffects) _soundEffects = soundEffects;
        else _soundEffects = _nativeSoundEffects = new NativeSoundEffects(diagnostics);
        _http = services.MultiplayerTransport is { } transport
            ? MultiplayerClientOptions.CreateHttpClient(transport)
            : MultiplayerClientOptions.CreateHttpClient();
        if (services.RunRandomState is { } runRandomState) _runRandomState = runRandomState;
        _startupSavePath = startupSavePath;
        _localDeviations = localDeviations;
        _referenceFrame = referenceFrame;
        _pointer = new(shape => _shell.ShowPointer(shape),
            () => ComputerTurnsCanRun() ? PresentationPointer.Idle(_state) : PointerShape.Arrow);
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
            if (OriginalSoundtrackPolicy.RestartsOnEntry(previous, current))
                _restartSoundtrackProgram = true;
            if (!KeepsGangSelection(current)) _gangSelection.Clear();
            // A pressed face acts on the screen it was pressed on; if something else moved the
            // screen during the wait, the key's action is dropped.
            _tickedPresentation.Clear();
            // FND-UI-062: a plain face a release left stays only until its panel goes.
            _releasedPanelFace = null;
            _citySectorClicks.Cancel();
            _sectorSiteClicks.Cancel();
            _sectorNeighborClicks.Cancel();
            _sectorRightClicks.Cancel();
            _sectorGangClicks.Cancel();
            _siteSearchClicks.Cancel();
            _heldSelectionFrame = HeldSelectionFrame(previous, current, _heldSelectionFrame, SelectionFrameShown());
            if (_slidePanels)
                _panelSlideTransition.Begin(previous, current, _inputTime, _gangDetailsCompact);
            foreach (var slot in AudioRouting.PanelTransitionSounds(
                previous, current, _slidePanels, _choosingCommandTarget))
                PlayGeneralSound(slot);
            // RULE-AWARDS-002: the endgame opens on its Awards tab.
            if (current == ClientScreen.Endgame && _state?.Outcome is not null)
                _showEndgameStats = false;
        };
        var userDataRoot = services.UserDataDirectory ?? _referenceFrame?.UserDataDirectory ?? Path.Combine(
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
        _warnIfIdleGangs = preferences.WarnIfIdleGangs && _referenceFrame?.IdleGangWarning != false;
        _selectedPlanningTimeLimit = preferences.PlanningTimeLimit;
        _showBaseStatistics = preferences.ShowBaseStatistics;
        _detailedCombat = preferences.DetailedCombat;
        _slidePanels = preferences.SlidePanels;
        _fullscreen = preferences.Fullscreen;
        _smoothEventSiteImages = preferences.SmoothEventSiteImages;
        _introMoviesSeen = preferences.IntroMoviesSeen;
        _introOnlyOnce = preferences.IntroOnlyOnce;
        _planningTimer.StopsInGameMenu = preferences.PlanningClockStopsInMenu;
        _defaultAiPolicy = preferences.DefaultAiPolicy;
        _preferredScenario = preferences.PreferredScenario;
        _online.Service = preferences.OnlineService;
        _online.Server.Set(preferences.CustomMultiplayerServer);
        _onlineLobbyPresentation = preferences.LobbyPresentation;
        _multiplayerRecoveries.AddRange(MultiplayerRecoveryStore.LoadAll(_multiplayerRecoveryPath));
        _onlineTokensInClear = MultiplayerRecoveryStore.KeepsTokensInClear(_multiplayerRecoveryPath);
        // The player's own name carries over from any saved seat, including one from another
        // session version; only the join code is limited to a match this build can play.
        if (_multiplayerRecoveries.FirstOrDefault(saved => saved.CanReconnect) is { } latest)
            _online.DisplayName.Set(latest.DisplayName);
        if (LatestOnlineRecovery is { } recovery)
        {
            _online.JoinCode.Set(recovery.JoinCode);
            if (recovery.ShouldSuggestReconnect)
                _message = "ONLINE MATCH INTERRUPTED  OPEN ONLINE TO RECONNECT";
        }
    }

    private GraphicsDevice GraphicsDevice => _shell.GraphicsDevice;

    /// <summary>Whether the window has the focus.</summary>
    private bool IsActive => _shell.IsActive;

    /// <summary>Closes the program; the shell asks <see cref="ConfirmExit"/> first.</summary>
    private void Exit() => _shell.Exit();

    /// <summary>Lets <paramref name="shell"/> run the game, before anything is loaded.</summary>
    internal void Attach(IGameShell shell) => _shell = shell ?? throw new ArgumentNullException(nameof(shell));

    /// <summary>Whether the window opens on the full screen, as the player last left it.</summary>
    internal bool StartsFullScreen => _fullscreen && _referenceFrame is null;

    /// <summary>Whether the window is a reference frame's, drawn at the virtual screen's size.</summary>
    internal bool DrawsReferenceFrame => _referenceFrame is not null;

    /// <summary>
    /// Loads the asset pack onto the graphics device and the audio device, then
    /// <see cref="LoadGameData"/>. The window calls this once its device exists.
    /// </summary>
    internal void LoadContent()
    {
        ValidateAssetPack();
        _batch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
        _eventSiteDitherOverlay = LastTurnEventPresentation.CreateEventSiteDitherOverlay(
            GraphicsDevice);

        _font = UiSprites is null
            ? throw new InvalidDataException("PX00129 is required for the original UI font.")
            : new PixelFont(GraphicsDevice, UiSprites);
        FindCombatAnimationFiles();
        if (!DecodesTexturesOnFirstDraw) DecodeAllTextures();
        _nativeSoundEffects?.LoadAll(_assetRoot);
        LoadGameData();
        _diagnostics?.Write("assets.loaded", new Dictionary<string, string?>
        {
            ["helpAvailable"] = (_helpDocument is not null).ToString(),
            ["combatSounds"] = (_nativeSoundEffects?.Count(SoundEffectBank.Combat) ?? 0).ToString(),
            ["generalSounds"] = (_nativeSoundEffects?.Count(SoundEffectBank.General) ?? 0).ToString(),
            ["combatAnimations"] = CombatAnimations.Count.ToString()
        });
    }

    /// <summary>
    /// Loads the original's data and the help, opens the save named on the command line and starts
    /// the intro. Everything the game plays with, and nothing it draws or sounds: a shell with no
    /// graphics device calls this alone.
    /// </summary>
    internal void LoadGameData()
    {
        _definitions = BundledOriginalData.Load();
        _helpDocument = ExtractedHelpStore.LoadOrNull(_assetRoot) is { } help
            ? HelpContentAugmentation.AddExecutableNotes(help)
            : null;
        // RULE-UI-013: the command-line file is opened before the intro test, which a loaded
        // match skips.
        OpenStartupSave();
        InitializeIntroMovies();
        // A reference frame is drawn and written before its Update ever reaches the soundtrack,
        // and opening every track took about a quarter of its run.
        if (_referenceFrame is null) LoadSoundtrack();
    }

    /// <summary>One tick of the game: the input since the last tick, the clocks and the screens.</summary>
    internal void Update(GameTime gameTime)
    {
        _autoSave.Pump();
        _inputTime = _referenceFrame is null ? gameTime.TotalGameTime : _referenceClock;
        _eventPump.Update(_inputTime, OutsideEventPump());
        if (UpdateReferenceFrame()) return;
        var keyboard = _shell.ReadKeyboard();
        var mouse = _shell.ReadMouse();
        // The rebuild's window shortcuts are not game events, so a fade does not swallow them.
        if (Pressed(keyboard, Keys.F12)) _screenshotRequested = true;
        // Alt+Enter goes no further, so the Enter does not also act on the screen.
        var altEnter = ShellWindow.AltEnter(keyboard, _previousKeyboard);
        if (ShellWindow.TogglesFullscreen(keyboard, _previousKeyboard)) ToggleFullscreen();
        // FND-AUDIO-016: the fade pumps window messages without game events.
        var soundtrackUpdated = _soundtrackFade is not null;
        if (soundtrackUpdated)
        {
            UpdateSoundtrack(gameTime);
            if (_soundtrackFade is not null)
            {
                UpdatePointerDuringFade(mouse);
                EndUpdate(keyboard, mouse);
                return;
            }
        }
        _multiSelectModifier = keyboard.IsKeyDown(Keys.LeftControl)
            || keyboard.IsKeyDown(Keys.RightControl);
        if (altEnter || UpdateIntroMovies(gameTime, keyboard, mouse))
        {
            EndUpdate(keyboard, mouse);
            return;
        }
        if (!soundtrackUpdated) UpdateSoundtrack(gameTime);
        if (_soundtrackFade is not null)
        {
            UpdatePointerDuringFade(mouse);
            EndUpdate(keyboard, mouse);
            return;
        }
        if (_replayViewer is not null)
        {
            UpdateReplayPlayback(gameTime, keyboard, mouse);
            EndUpdate(keyboard, mouse);
            return;
        }
        UpdateComlinkAlert(_eventPump.Time);
        UpdateComlinkCaret(_eventPump.Time);
        PumpBugReportSend();
        // Before the planning timer, so a turn that resolved on the server is adopted even on the
        // frame the local clock would otherwise have taken over the loop.
        UpdateOnlineSession();
        // SCR-UI-002: the credits hold every input until a click or a key closes them.
        if (_creditsOpen)
        {
            UpdateCredits(keyboard, mouse);
            EndUpdate(keyboard, mouse);
            return;
        }
        var rightClicked = PointerButtonEdges.Pressed(
            mouse.RightButton, _previousMouse.RightButton);
        if (!_gameMenuOpen && UpdatePlanningTimer(gameTime.TotalGameTime))
        {
            EndUpdate(keyboard, mouse);
            return;
        }
        RunComputerTurns();
        CaptureNewCombatAnimations();
        foreach (var clip in _combatAnimationPlayer.Advance(gameTime.ElapsedGameTime, _combatExit.Tracking))
            if (clip.Sound is { } soundIndex) PlayCombatSound(soundIndex);
        if (_combatAnimationPlayer.IsPlaying)
        {
            var exitPointMapped = VirtualInput.TryMap(
                _shell.Viewport, mouse.Position, out var exitPoint);
            var pointer = _combatExit.Update(exitPointMapped ? exitPoint : null,
                mouse.LeftButton == ButtonState.Pressed,
                _previousMouse.LeftButton == ButtonState.Pressed);
            if (pointer == DetailedCombatPointerResult.Rejected)
                PlayGeneralSound(GeneralSoundSlot.RejectedInput);
            // Escape and a release on the Exit face end the whole presentation (SCR-COMBAT-002);
            // the right button does too (DEV-COMBAT-001).
            if (Pressed(keyboard, Keys.Escape)
                || pointer == DetailedCombatPointerResult.EndPresentation || rightClicked)
            {
                // The cue belongs to the clip being skipped, and the original unloads slot 5 once
                // a combatant's sequence ends, so it does not outlive the presentation.
                _combatAnimationPlayer.Clear();
                _combatExit.Reset();
                StopEffectVoice();
                _message = string.Empty;
            }
            // The key or button that ended the presentation does nothing else this frame, so
            // Escape does not also open the game menu.
            EndUpdate(keyboard, mouse);
            return;
        }
        _combatExit.Reset();
        if (_automaticDetailedCombatPresentation)
            FinishAutomaticCombatPresentation();
        // RULE-TIMER-004: the original handles no message while a pressed face or a flash waits
        // on the presentation clock, so this frame's keys and clicks are dropped.
        if (UpdateTickedPresentation())
        {
            EndUpdate(keyboard, mouse);
            return;
        }
        if (rightClicked && !_gameMenuOpen)
            CancelCurrentInteraction(
                VirtualInput.TryMap(_shell.Viewport, mouse.Position, out var rightPoint) ? rightPoint : null);
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
                var shortcut = ShellWindow.ShortcutFor(keyboard, _previousKeyboard);
                if (shortcut == ShellShortcut.Credits) OpenCredits();
                else if (shortcut == ShellShortcut.Help) OpenHelp();
                else if (shortcut == ShellShortcut.Options) OpenOptions();
                else if (shortcut == ShellShortcut.Escape && CommandPanelOpen)
                    CancelCommandPanelWithEscape();
                else if (shortcut == ShellShortcut.Escape)
                {
                    // SCR-ATTACK-001, FND-ATTACK-003: Escape is the Attack picker's Cancel.
                    if (IsAttackPickerOpen()) CancelAttackPickerByKey();
                    else if (_configuringOnlineLobby) CloseOnlineSetup();
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
                    if (Pressed(keyboard, Keys.A)) _showEndgameStats = false;
                    if (Pressed(keyboard, Keys.S)) _showEndgameStats = true;
                    if (Pressed(keyboard, Keys.Enter)) LeaveEndgame();
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
                    // A step started this frame holds its key face; input during the wait is
                    // dropped (RULE-TIMER-004), so the panel does not close under it.
                    if (!_tickedPresentation.Active
                        && (Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Delete)
                            || Pressed(keyboard, Keys.Back)))
                        AcceptAndInvoke(CloseEvents);
                    break;
                case ClientScreen.ComlinkView:
                    UpdateComlinkView(keyboard);
                    break;
                case ClientScreen.Commands:
                    if (IsAttackPickerOpen())
                    {
                        UpdateAttackPicker(keyboard);
                        break;
                    }
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
                    if (Pressed(keyboard, Keys.Enter)
                        || (CommandPanelOpen && Pressed(keyboard, Keys.Execute)))
                        ConfirmCommandsByKey();
                    if (Pressed(keyboard, Keys.Back))
                        AcceptAndInvoke(BackFromCommands);
                    break;
                case ClientScreen.Hire:
                    if (Pressed(keyboard, Keys.Left) || Pressed(keyboard, Keys.Up)) MoveHireCursor(-1);
                    if (Pressed(keyboard, Keys.Right) || Pressed(keyboard, Keys.Down)) MoveHireCursor(1);
                    if (Pressed(keyboard, Keys.S)) SnubSelectedHireOffer();
                    if (Pressed(keyboard, Keys.Back)) AcceptAndShow(_managementReturnScreen);
                    // FND-HIRE-009: Enter or Execute presses the OK face for one tick, then closes.
                    else if (PressedEnterOrExecute(keyboard))
                        PressKeyFace(PressedKeyFace.Confirm, HireComparisonLayout.Ok.Location,
                            () => _screens.Show(_managementReturnScreen));
                    break;
                case ClientScreen.Sector:
                    UpdateSector(keyboard);
                    break;
                case ClientScreen.SectorGangs:
                    if (Pressed(keyboard, Keys.Left) || Pressed(keyboard, Keys.Up))
                        MoveSectorGangCursor(-1);
                    if (Pressed(keyboard, Keys.Right) || Pressed(keyboard, Keys.Down))
                        MoveSectorGangCursor(1);
                    // SCR-UI-005: Enter or Execute presses the close face.
                    if (Pressed(keyboard, Keys.Back) || PressedEnterOrExecute(keyboard))
                        AcceptAndInvoke(CloseSectorGangs);
                    break;
                case ClientScreen.Gang:
                    if (_gangDetailsInstanceId is not null)
                    {
                        if (Pressed(keyboard, Keys.Left) || Pressed(keyboard, Keys.Up)) CycleGangDetails(-1);
                        if (Pressed(keyboard, Keys.Right) || Pressed(keyboard, Keys.Down)) CycleGangDetails(1);
                    }
                    // SCR-GANG-001, SCR-GANG-002: Enter or Execute presses the close face for one
                    // tick before the panel closes (FND-GANG-006, FND-GANG-010).
                    if (Pressed(keyboard, Keys.Back)) AcceptAndInvoke(CloseGangDetails);
                    else if (PressedEnterOrExecute(keyboard))
                        PressKeyFace(PressedKeyFace.Confirm, GangDetailsOk.Location, CloseGangDetails);
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
                    // SCR-UI-008: Enter or Execute closes the panel.
                    if (Pressed(keyboard, Keys.Back) || PressedEnterOrExecute(keyboard))
                        AcceptAndInvoke(CloseGameInformation);
                    break;
                case ClientScreen.Finance:
                    // SCR-FINANCE-001: Enter or Execute presses the close control.
                    if (Pressed(keyboard, Keys.Back) || PressedEnterOrExecute(keyboard))
                        AcceptAndShow(_managementReturnScreen);
                    break;
                case ClientScreen.Ranking:
                    // FND-OBJECTIVE-005: Enter or Execute presses the button for one tick, then
                    // closes.
                    if (Pressed(keyboard, Keys.Back)) AcceptAndShow(_managementReturnScreen);
                    else if (PressedEnterOrExecute(keyboard))
                        PressKeyFace(PressedKeyFace.Confirm, PlayerRankingLayout.Ok.Location,
                            () => _screens.Show(_managementReturnScreen));
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
                    if (Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Execute))
                        ConfirmCommandPanelByKey();
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
                    if (Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Execute))
                        ConfirmCommandPanelByKey();
                    if (Pressed(keyboard, Keys.Back))
                        AcceptAndInvoke(CloseSellEquipment);
                    break;
                case ClientScreen.CombatSummary:
                    // Left and Right page and Enter or Execute (0x2B) closes; the panel handles no
                    // other key, Escape included (SCR-COMBAT-001). D replays the selected fight
                    // (DEV-COMBAT-002).
                    if (Pressed(keyboard, Keys.Left)) MoveCombatSummary(-1);
                    if (Pressed(keyboard, Keys.Right)) MoveCombatSummary(1);
                    if (Pressed(keyboard, Keys.D)) ReplaySelectedCombatDetail();
                    if (Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Execute))
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
                    if (Pressed(keyboard, Keys.Back)) CloseSiteSearch();
                    break;
            }
        }
        // F10 opened the replay viewer this frame. _state is now a historical frame, so the click
        // dispatch and the combat-presentation capture below must not run against it.
        if (_replayViewer is not null)
        {
            EndUpdate(keyboard, mouse);
            return;
        }
        // RULE-UI-003: the original handles no message while a panel slides in, and takes a click
        // made during the slide from the queue once the panel is in place. The pointer is
        // therefore read against the panel's final place, whatever the drawn offset.
        var pointerMapped = VirtualInput.TryMap(_shell.Viewport, mouse.Position, out var virtualPoint);
        UpdateHoverPoint(pointerMapped ? virtualPoint : null);
        var wheelDelta = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        if (pointerMapped && _screens.Current == ClientScreen.Help && wheelDelta != 0)
            HandleHelpScroll(virtualPoint, wheelDelta);
        if (pointerMapped && mouse.LeftButton == ButtonState.Pressed)
        {
            _dragPoint = virtualPoint;
            if (_previousMouse.LeftButton == ButtonState.Released) HandleClick(virtualPoint);
            else HoldPointerAt(virtualPoint);
        }
        if (_previousMouse.LeftButton == ButtonState.Pressed && mouse.LeftButton == ButtonState.Released)
        {
            _leftHoldOutlivesCancel = false;
            CompletePointerRelease(pointerMapped, virtualPoint, rightButton: false);
        }
        if (_previousMouse.RightButton == ButtonState.Pressed && mouse.RightButton == ButtonState.Released)
        {
            _rightHoldOutlivesCancel = false;
            CompletePointerRelease(pointerMapped, virtualPoint, rightButton: true);
        }
        CaptureNewCombatAnimations();
        EndUpdate(keyboard, mouse);
    }

    /// <summary>Records this frame's input as the previous frame's, which every edge test reads.</summary>
    private void EndUpdate(KeyboardState keyboard, MouseState mouse)
    {
        ReleaseSkippedPanelFace(mouse);
        FlushScenarioPreference();
        _pointer.Refresh();
        _previousKeyboard = keyboard;
        _previousMouse = mouse;
    }

    private void HandleClick(Point point)
    {
        // FND-UI-063: the console tile helper loops until the right button that pressed it comes up,
        // and a left press meanwhile reaches nothing.
        if (_pressedCityConsoleControl is not null && _pressedCityConsoleByRightButton) return;
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
                if (TitleActionAt(point) is { } titleAction) RunTitleAction(titleAction);
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
                if (PressSetupName(point)) break;
                var timed = ScenarioCatalog.Get(_selectedScenario).IsTimed;
                var panelControl = SetupPanelLayout.HitTest(point, timed);
                var durationRefused = !timed && SetupPanelLayout.DurationArea.Contains(point);
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
                else if (panelControl is { } control)
                    BeginSetupPanelControl(control);
                else if (durationRefused)
                    RejectInput(ObjectiveDurationWarning);
                else if (setupPlayer >= 0) BeginSetupPlayerDrag(setupPlayer, point);
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
                // SCR-UI-005: the face closes on a release inside it; a press outside the panel
                // is refused, and a press elsewhere inside it does nothing.
                PressPanelFace(point, SectorGangsLayout.Panel, SectorGangsLayout.Ok, CloseSectorGangs);
                break;
            case ClientScreen.Gang:
                if (HandleGangDetailsEquipmentClick(point)) break;
                // SCR-GANG-001, SCR-GANG-002: the same face and outside test.
                PressPanelFace(point, GangDetailsPanel, GangDetailsOk, CloseGangDetails);
                break;
            case ClientScreen.Site:
                // SCR-UI-007, FND-UI-067: the held close face closes on a release inside it.
                PressPanelFace(point, SiteInformationLayout.Panel, SiteInformationLayout.Ok, CloseSiteDetails);
                break;
            case ClientScreen.ItemInformation:
                // SCR-UI-006, FND-UI-047: the held exit face closes on a release inside it.
                PressPanelFace(point, ItemInformationLayout.Panel, ItemInformationLayout.Ok, CloseItemDetails);
                break;
            case ClientScreen.GameInfo:
                // SCR-UI-008: the OK face closes the panel; a press outside it is refused.
                PressPanelFace(point, GameInformationLayout.InputBounds, GameInformationLayout.Ok,
                    CloseGameInformation);
                break;
            case ClientScreen.Finance:
                // SCR-FINANCE-001, FND-UI-067: the press is tested against the shared panel
                // rectangle, wider than the panel drawn.
                PressPanelFace(point, SharedPanelLayout.Panel, FinanceLayout.Ok,
                    () => _screens.Show(_managementReturnScreen));
                break;
            case ClientScreen.Ranking:
                // SCR-OBJECTIVE-001, FND-UI-067.
                PressPanelFace(point, PlayerRankingLayout.Panel, PlayerRankingLayout.Ok,
                    () => _screens.Show(_managementReturnScreen));
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
        if (_state is null || PlanningViewer is not { } playerId) return;
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

    private bool Pressed(KeyboardState current, Keys key) => current.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

}

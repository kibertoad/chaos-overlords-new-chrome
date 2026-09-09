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
    private static readonly Rectangle EventsDismiss = new(218, 414, 96, 28);
    private static readonly Rectangle EventsBack = new(322, 414, 96, 28);
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
    private Texture2D? _endgameBackground;
    private Texture2D? _handoffPanel;
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

        _titleBackground = LoadTexture("PX00130.bmp");
        _setupBackground = LoadTexture("PX00143.bmp");
        _cityBackground = LoadTexture("PX00128.bmp");
        _endgameBackground = LoadTexture("PX00200.bmp");
        _handoffPanel = LoadTexture("PX00132.bmp");
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
        }
    }

    private void HandleCityClick(Point point)
    {
        const int left = 10, top = 49, cellWidth = 52, cellHeight = 42;
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
        else if (CityEvents.Contains(point)) _screens.Show(ClientScreen.Events);
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
        batch.Draw(pixel, new Rectangle(8, 7, 420, 29), new Color(0, 0, 0, 205));
        font.Draw(batch, $"TURN {state.Coordinator.Turn}  {player.Setup.Name}  ${player.Cash}",
            new Vector2(16, 17), Color.White, 1);

        const int left = 10, top = 49, cellWidth = 52, cellHeight = 42;
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

        batch.Draw(pixel, new Rectangle(8, 389, 420, 60), new Color(0, 0, 0, 220));
        font.Draw(batch, SectorSummary(state, state.Sectors[_cursor]), new Vector2(14, 395), Color.White, 1);
        font.Draw(batch, $"GANGS {player.Gangs.Count}/{MatchLimits.GangsPerPlayer}", new Vector2(14, 410), PlayerColors[playerIndex], 1);
        font.Draw(batch, _message, new Vector2(112, 410), Color.Gold, 1);
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
            _screens.Show(ClientScreen.Handoff);
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

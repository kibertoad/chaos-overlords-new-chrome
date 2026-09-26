using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private AiDifficulty _selectedAiMentality = AiDifficulty.Criminal;
    private AiPolicyMode _defaultAiPolicy = OriginalOptionsPolicy.AiPolicyByDefault;
    private static readonly Rectangle TitleNewGame = new(220, 292, 200, 34);
    private static readonly Rectangle TitleLoadGame = new(220, 334, 98, 34);
    private static readonly Rectangle TitleOnline = new(322, 334, 98, 34);
    private static readonly Rectangle TitleOptions = new(154, 376, 80, 34);
    private static readonly Rectangle TitleHelp = new(238, 376, 80, 34);
    private static readonly Rectangle TitleIntro = new(322, 376, 80, 34);
    private static readonly Rectangle TitleQuit = new(406, 376, 80, 34);

    /// <summary>
    /// The strip right of the menu, where a notice can be read without covering a button.
    /// </summary>
    /// <remarks>
    /// Notices arrive on this screen from somewhere else (a match that ended, an online session that
    /// was left), so they are as long as whatever happened, and a centred line of that length ran
    /// straight through the row of buttons underneath it.
    /// </remarks>
    private static readonly Rectangle TitleMessage = new(494, 292, 138, 118);

    /// <summary>How far the version line stays clear of the right edge of the title screen.</summary>
    private const int TitleVersionMargin = 6;

    private const string ObjectiveDurationWarning =
        "OBJECTIVES DISABLE TIME LIMITS";

    private void UpdateTitle(KeyboardState keyboard)
    {
        if (Pressed(keyboard, Keys.Enter)) OpenNewGameSetup();
        if (Pressed(keyboard, Keys.F9)) OpenSaveBrowser(saving: false, fromTitle: true);
    }

    internal enum TitleAction
    {
        NewGame,
        LoadGame,
        Online,
        Options,
        Help,
        Intro,
        Quit
    }

    /// <summary>What a left press at <paramref name="point"/> on the title screen does.</summary>
    /// <remarks>
    /// The original's title loop turns a left press anywhere into New Game (RULE-UI-013,
    /// SCR-UI-001). The rebuild's own buttons keep their commands, and a press anywhere else still
    /// starts a new game.
    /// </remarks>
    internal static TitleAction TitleActionAt(Point point) =>
        TitleLoadGame.Contains(point) ? TitleAction.LoadGame
        : TitleOnline.Contains(point) ? TitleAction.Online
        : TitleOptions.Contains(point) ? TitleAction.Options
        : TitleHelp.Contains(point) ? TitleAction.Help
        : TitleIntro.Contains(point) ? TitleAction.Intro
        : TitleQuit.Contains(point) ? TitleAction.Quit
        : TitleAction.NewGame;

    /// <summary>
    /// RULE-SETUP-002: the stored scenario and a one-year limit. RULE-SETUP-010: the roster of the
    /// last Begin of the session, or one human in slot 0 before the first.
    /// </summary>
    private void OpenNewGameSetup()
    {
        _selectedScenario = _preferredScenario;
        _selectedDuration = GameDuration.OneYear;
        var roster = _begunLocalSetup ?? LocalSetupSnapshot.Initial;
        _localSetupRoster.Restore(roster.HumanSlots);
        for (var slot = 0; slot < MatchLimits.PlayerCount; slot++)
        {
            _playerPortraits[slot] = roster.Portraits[slot];
            _playerNames[slot] = roster.Names[slot];
        }
        _selectedSetupPlayerSlot = roster.HumanSlots.Min();
        _screens.Show(ClientScreen.Setup);
    }

    /// <summary>RULE-SETUP-002: a committed scenario choice on the local setup is stored.</summary>
    private void CommitScenario(ScenarioId scenario)
    {
        _selectedScenario = scenario;
        if (_configuringOnlineLobby) return;
        _preferredScenario = scenario;
        SavePreferences();
    }

    private void UpdateSetup(KeyboardState keyboard)
    {
        if (Pressed(keyboard, Keys.Left)) ChangeScenario(-1);
        if (Pressed(keyboard, Keys.Right)) ChangeScenario(1);
        if (Pressed(keyboard, Keys.Up)) ChangeDuration(1);
        if (Pressed(keyboard, Keys.Down)) ChangeDuration(-1);
        if (!_configuringOnlineLobby && Pressed(keyboard, Keys.OemMinus)) ChangePlayerCount(-1);
        if (!_configuringOnlineLobby && Pressed(keyboard, Keys.OemPlus)) ChangePlayerCount(1);
        if (Pressed(keyboard, Keys.M)) CycleDifficulty();
        if (Pressed(keyboard, Keys.L)) CyclePlanningTimeLimit();
        if (Pressed(keyboard, Keys.Enter))
        {
            if (_configuringOnlineLobby) SaveOnlineSetup();
            else StartMatch();
        }
    }

    /// <summary>
    /// RULE-RNG-001, DEV-RNG-001: where the run's one random sequence stands while no local match
    /// holds it. It starts from the clock once per run; each local match draws on from it and
    /// hands it back when it is left, so a second New Game continues the sequence as in the
    /// original.
    /// </summary>
    private uint _runRandomState = unchecked((uint)DeterministicRandom.SeedFromTimerMilliseconds(
        unchecked((uint)Environment.TickCount)));

    /// <summary>Takes the run's sequence back from a local match that is being left.</summary>
    private void KeepRunRandomState()
    {
        if (_state is not null && HotSeatJournal is not null) _runRandomState = _state.Random.State;
    }

    private void ChangeScenario(int delta)
    {
        var currentButton = SetupScenarioButtons.ButtonForScenario(_selectedScenario);
        CommitScenario(SetupScenarioButtons.ScenarioForButton(
            Mod(currentButton + delta, SetupScenarioButtons.VisualOrder.Count)));
        PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
        _message = string.Empty;
    }

    private void ChangeDuration(int delta)
    {
        if (!ScenarioCatalog.Get(_selectedScenario).IsTimed)
        {
            RejectInput(ObjectiveDurationWarning);
            return;
        }
        _selectedDuration = Durations[Mod(Array.IndexOf(Durations, _selectedDuration) + delta, Durations.Length)];
        PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
        _message = string.Empty;
    }

    private void ChangePlayerCount(int delta, bool pointerButton = false)
    {
        var changed = delta switch
        {
            > 0 => AddSetupHuman(),
            < 0 => RemoveSetupHuman(),
            _ => false
        };
        if (AudioRouting.PlayerCountResultSound(
                changed, pointerButton) is { } slot)
            PlayGeneralSound(slot);
    }

    private bool AddSetupHuman()
    {
        // RULE-SETUP-010: the lowest portrait no other human holds; the name stays as it was.
        var portrait = LocalSetupPolicy.LowestFreePortrait(_playerPortraits, _localSetupRoster.HumanSlots);
        if (_localSetupRoster.AddHuman() is not { } added) return false;
        _playerPortraits[added] = portrait;
        _selectedSetupPlayerSlot = added;
        return true;
    }

    private bool RemoveSetupHuman()
    {
        if (_localSetupRoster.RemoveHuman(_selectedSetupPlayerSlot) is not { } removed) return false;
        if (_editingPlayerName == removed) FinishSetupNameEdit(cancel: true);
        _selectedSetupPlayerSlot = _localSetupRoster.HumanSlots.Max();
        return true;
    }

    /// <summary>SCR-SETUP-001, FND-SETUP-013: a left-panel press shows the pressed image and plays
    /// the push cue; the choice is taken only when the button is released inside the control.</summary>
    private void BeginSetupPanelControl(SetupPanelControl control)
    {
        _pressedSetupPanelControl = control;
        PlayGeneralSound(GeneralSoundSlot.ButtonPress);
        _message = string.Empty;
    }

    private void CompleteSetupPanelControl(Point point)
    {
        var pressed = _pressedSetupPanelControl;
        _pressedSetupPanelControl = null;
        if (_screens.Current != ClientScreen.Setup
            || pressed is not { } control
            || !SetupPanelLayout.Destination(control).Contains(point))
            return;
        switch (control.Kind)
        {
            case SetupPanelControlKind.Scenario:
                CommitScenario(SetupScenarioButtons.ScenarioForButton(control.Index));
                break;
            case SetupPanelControlKind.Duration:
                _selectedDuration = Durations[control.Index];
                break;
            case SetupPanelControlKind.AiMentality:
                _selectedAiMentality = (AiDifficulty)control.Index;
                break;
            case SetupPanelControlKind.PlanningTime:
                _selectedPlanningTimeLimit = (PlanningTimeLimit)control.Index;
                SavePreferences();
                break;
        }
        _message = string.Empty;
    }

    private void BeginSetupButton(SetupPushButton button)
    {
        _pressedSetupButton = button;
        PlayGeneralSound(GeneralSoundSlot.ButtonPress);
    }

    private void CompleteSetupButton(Point point)
    {
        var pressed = _pressedSetupButton;
        _pressedSetupButton = null;
        if (_screens.Current != ClientScreen.Setup
            || pressed is null
            || SetupButtonLayout.HitTest(point) != pressed)
            return;
        switch (pressed)
        {
            // The roster, and the start of the match itself, belong to the lobby when one is open.
            // Only the rules a solo game also chooses are set here, and CONFIRM carries them back.
            case SetupPushButton.AddPlayer:
                if (!_configuringOnlineLobby) ChangePlayerCount(1, pointerButton: true);
                break;
            case SetupPushButton.RemovePlayer:
                if (!_configuringOnlineLobby) ChangePlayerCount(-1, pointerButton: true);
                break;
            case SetupPushButton.Start:
                if (!_configuringOnlineLobby) StartMatch();
                break;
            case SetupPushButton.Back:
                if (_configuringOnlineLobby) SaveOnlineSetup();
                else _screens.Show(ClientScreen.Title);
                break;
        }
    }

    private void BeginSetupNameEdit(int index)
    {
        if (_configuringOnlineLobby) return;
        if (!_localSetupRoster.IsHuman(index)) return;
        if (_editingPlayerName is not null) FinishSetupNameEdit(cancel: false);
        _editingPlayerName = index;
        _setupOriginalName = _playerNames[index];
        // The native modal editor begins with an empty buffer. Confirming it without entering a
        // character leaves the existing 12-byte seat record untouched rather than restoring a
        // generated default name.
        _setupNameEditor.Begin(string.Empty);
        _message = string.Empty;
    }

    private void UpdateSetupName(KeyboardState keyboard)
    {
        if (_editingPlayerName is null) return;
        if (Pressed(keyboard, Keys.Escape))
        {
            FinishSetupNameEdit(cancel: true);
            return;
        }
        if (Pressed(keyboard, Keys.Enter))
        {
            FinishSetupNameEdit(cancel: false);
            return;
        }
        if (Pressed(keyboard, Keys.Back))
        {
            _setupNameEditor.Backspace();
            return;
        }

        var shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        foreach (var key in keyboard.GetPressedKeys())
        {
            if (_previousKeyboard.IsKeyDown(key)) continue;
            if (OriginalTextInput.TryCharacter(key, shift, out var character))
                _setupNameEditor.TryAppend(character);
        }
    }

    private void FinishSetupNameEdit(bool cancel)
    {
        if (_editingPlayerName is not { } index) return;
        var entered = _setupNameEditor.Text;
        _playerNames[index] = cancel
            ? _setupOriginalName
            : LocalSetupPolicy.NameAfterModalEntry(_setupOriginalName, entered);
        _editingPlayerName = null;
        _message = string.Empty;
    }

    private void CycleDifficulty()
    {
        var values = Enum.GetValues<AiDifficulty>();
        _selectedAiMentality = values[Mod(
            Array.IndexOf(values, _selectedAiMentality) + 1, values.Length)];
        PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
        _message = string.Empty;
    }

    private void CyclePortrait(int player, int delta)
    {
        if (_configuringOnlineLobby) return;
        if (!_localSetupRoster.IsHuman(player)) return;
        _playerPortraits[player] = LocalSetupPolicy.StepPortrait(
            _playerPortraits, _localSetupRoster.HumanSlots, player, delta);
        PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
        _message = string.Empty;
    }

    private void BeginSetupPlayerDrag(int player, Point point)
    {
        if (_configuringOnlineLobby) return;
        if (!_localSetupRoster.IsHuman(player)) return;
        _draggedSetupPlayerSlot = player;
        _setupPlayerPressPoint = point;
        _dragPoint = point;
        _setupPlayerDragStarted = false;
    }

    private void CompleteSetupPlayerDrag(Point point)
    {
        if (_draggedSetupPlayerSlot is not { } source)
        {
            CancelSetupPlayerDrag();
            return;
        }
        var target = HitTest.IndexAt(MatchLimits.PlayerCount, PlayerPortraitLayout.SetupHit, point);
        var result = _localSetupRoster.MoveHuman(source, target);
        if (result is LocalSetupMoveResult.MovedToEmptyColor
            or LocalSetupMoveResult.ExchangedHumanColors)
        {
            (_playerNames[source], _playerNames[target]) =
                (_playerNames[target], _playerNames[source]);
            (_playerPortraits[source], _playerPortraits[target]) =
                (_playerPortraits[target], _playerPortraits[source]);
            PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
            _message = string.Empty;
        }
        if (result != LocalSetupMoveResult.Invalid)
            _selectedSetupPlayerSlot = target;
        else
        {
            PlayGeneralSound(GeneralSoundSlot.RejectedInput);
            _message = string.Empty;
        }
        CancelSetupPlayerDrag();
    }

    private void CompleteSetupPlayerClick()
    {
        if (_draggedSetupPlayerSlot is not { } player)
        {
            CancelSetupPlayerDrag();
            return;
        }

        switch (PlayerPortraitLayout.ClickAction(
                    _selectedSetupPlayerSlot, player, _setupPlayerPressPoint))
        {
            case SetupPlayerCardClick.Select:
                _selectedSetupPlayerSlot = player;
                break;
            case SetupPlayerCardClick.PreviousPortrait:
                CyclePortrait(player, -1);
                break;
            case SetupPlayerCardClick.NextPortrait:
                CyclePortrait(player, 1);
                break;
            case SetupPlayerCardClick.EditName:
                BeginSetupNameEdit(player);
                break;
        }
        CancelSetupPlayerDrag();
    }

    private void CancelSetupPlayerDrag()
    {
        _draggedSetupPlayerSlot = null;
        _setupPlayerDragStarted = false;
    }

    private void StartMatch()
    {
        if (_definitions is null) return;
        // RULE-SETUP-010: Begin saves the roster for the next local setup; Cancel saves nothing.
        _begunLocalSetup = new LocalSetupSnapshot(
            _localSetupRoster.HumanSlots.ToArray(), _playerPortraits.ToArray(), _playerNames.ToArray());
        // The original shows the hourglass while it sets up the city (RULE-UI-007).
        using var busy = _pointer.Busy();
        var players = _localSetupRoster.HumanSlots.Order()
            .Select(slot => new MatchPlayerSetup(
                new PlayerId(slot), _playerNames[slot],
                PlayerController.Human,
                _playerPortraits[slot]))
            .ToArray();
        KeepRunRandomState();
        var setup = new MatchSetup(
            _selectedScenario, _selectedDuration, unchecked((int)_runRandomState), players,
            _selectedAiMentality, allowSparsePlayerIds: true,
            aiPolicy: _defaultAiPolicy);
        _diagnostics?.Write("match.started", new Dictionary<string, string?>
        {
            ["scenario"] = _selectedScenario.ToString(),
            ["duration"] = _selectedDuration.ToString(),
            ["configuredPlayers"] = _localSetupRoster.Count.ToString(),
            ["computerPlayers"] = "0",
            ["mentality"] = _selectedAiMentality.ToString(),
            ["aiPolicy"] = _defaultAiPolicy.ToString(),
            ["seed"] = setup.InitialSeed.ToString()
        });
        _state = OriginalMatchFactory.Create(_definitions, setup);
        _actions = new MatchActions(new MatchReplayRecorder(_state));
        ResetHotSeatEliminationPresentation(acknowledgeExistingEliminations: false);
        if (!_debugPhaseStepping) GameplayTurnFlow.AdvanceToPlanning(_actions.HotSeatRecorder);
        if (!_debugPhaseStepping) PrepareCurrentHireOffers();
        _cursor = _state.Players[0].Gangs[0].SectorId;
        _selectedGangIndex = 0;
        _message = string.Empty;
        _combatPresentationProgress.Clear();
        ResetTransientMatchUi();
        _siteSearchSelections.Reset();
        _lastTurnEventArchive.Clear();
        _managementReturnScreen = ClientScreen.City;
        // RULE-SETUP-008: a new game never opens Game Information; only a loaded one does.
        _resumedMatchTurn = null;
        _continuePlanningEntryAfterGameInfo = false;
        _deferComlinkAlertUntilPlanningVisible = false;
        PresentHotSeatPlanningEntry();
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
        DrawButton(batch, pixel, font, TitleLoadGame, "LOAD", true);
        DrawButton(batch, pixel, font, TitleOnline, "ONLINE", true);
        DrawButton(batch, pixel, font, TitleOptions, "OPTIONS", true);
        DrawButton(batch, pixel, font, TitleHelp, "HELP", true);
        DrawButton(batch, pixel, font, TitleIntro, "INTRO", true);
        DrawButton(batch, pixel, font, TitleQuit, "QUIT", true);
        DrawCentered(font, batch, "NEW CHROME", 282, new Color(210, 52, 43), 1);
        DrawTitleMessage(batch, pixel, font);
        DrawCentered(font, batch, "RESTORED BY KIBERTOAD", 430,
            new Color(185, 195, 195), 1);
        DrawTitleVersion(batch, font);
    }

    /// <summary>
    /// Prints the build's version in the corner the credit line leaves free.
    /// </summary>
    /// <remarks>
    /// The title screen is the one place every player passes through, so it is where the number a
    /// bug report will be filed against has to be readable without opening anything.
    /// </remarks>
    private static void DrawTitleVersion(SpriteBatch batch, PixelFont font)
    {
        var width = GameVersion.Display.Length * OriginalFontLayout.CellWidth;
        font.Draw(batch, GameVersion.Display,
            new Vector2(VirtualInput.Width - width - TitleVersionMargin, 430),
            new Color(150, 160, 160), 1);
    }

    /// <summary>Draws whatever the last screen left to say, wrapped into the margin.</summary>
    private void DrawTitleMessage(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (string.IsNullOrWhiteSpace(_message)) return;
        var columns = TitleMessage.Width / OriginalFontLayout.CellWidth;
        var lines = HelpTextLayout.Wrap(_message, columns)
            .Take(TitleMessage.Height / OriginalFontLayout.LineHeight)
            .ToArray();
        var panel = new Rectangle(TitleMessage.X - 5, TitleMessage.Y - 5, TitleMessage.Width + 10,
            lines.Length * OriginalFontLayout.LineHeight + 9);
        batch.Draw(pixel, panel, new Color(0, 0, 0, 200));
        for (var row = 0; row < lines.Length; row++)
            font.Draw(batch, lines[row],
                new Vector2(TitleMessage.X, TitleMessage.Y + row * OriginalFontLayout.LineHeight),
                new Color(185, 195, 195), 1);
    }

    /// <summary>
    /// SCR-SETUP-001, FND-SETUP-013: the setup lights are the 8x16 light of the controls sheet; the
    /// lit rectangle is its 3x11 core, one pixel right and three down from the sprite's corner.
    /// </summary>
    private void DrawSetupLight(SpriteBatch batch, Texture2D pixel, Rectangle lit)
    {
        if (_setupControls is not null)
            batch.Draw(_setupControls, new Rectangle(lit.X - 1, lit.Y - 3,
                SetupPanelLayout.LightSource.Width, SetupPanelLayout.LightSource.Height),
                SetupPanelLayout.LightSource, Color.White);
        else DrawSelectionLight(batch, pixel, lit);
    }

    private void DrawSetup(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (_setupBackground is not null)
            batch.Draw(_setupBackground, new Rectangle(0, 0, 640, 460), Color.White);
        else
            batch.Draw(pixel, new Rectangle(70, 52, 500, 384), new Color(0, 0, 0, 220));
        if (_setupControls is not null
            && _pressedSetupButton is { } pressed
            && _hoverPoint is { } buttonHover
            && SetupButtonLayout.HitTest(buttonHover) == pressed)
            batch.Draw(_setupControls, SetupButtonLayout.Destination(pressed),
                SetupButtonLayout.PressedSource(pressed), Color.White);
        DrawSetupLight(batch, pixel, OriginalSelectionLightLayout.Scenario(
            SetupScenarioButtons.ButtonForScenario(_selectedScenario)));
        if (ScenarioCatalog.Get(_selectedScenario).IsTimed)
            DrawSetupLight(batch, pixel, OriginalSelectionLightLayout.Duration(
                Array.IndexOf(Durations, _selectedDuration)));
        var onlinePlayers = _configuringOnlineLobby
            ? _online.Match?.Players.Where(player => player.Status == Rechaos.Multiplayer.Generated.PlayerStatus.Active)
                .Take(MatchLimits.PlayerCount).ToArray() ?? []
            : [];
        // A seated player wears the face they chose on their way in; the portraits stored with the
        // rules dress the seats nobody claimed, which are the ones the computer will play.
        var portraits = Enumerable.Range(0, MatchLimits.PlayerCount)
            .Select(index => _configuringOnlineLobby && index < onlinePlayers.Length
                ? OnlinePortrait(onlinePlayers[index])
                : _playerPortraits[index])
            .ToArray();
        for (var index = 0; index < MatchLimits.PlayerCount; index++)
        {
            var active = _configuringOnlineLobby
                ? index < onlinePlayers.Length
                : _localSetupRoster.IsHuman(index);
            if (_uiSprites is not null)
                batch.Draw(_uiSprites, PlayerPortraitLayout.SetupTop(index),
                    OriginalSpriteLayout.OverlordPortrait(
                        active ? portraits[index] : PlayerPortraitLayout.Count - 1),
                    Color.White);
        }
        var shownHumans = _configuringOnlineLobby
            ? Enumerable.Range(0, onlinePlayers.Length)
            : _localSetupRoster.HumanSlots;
        foreach (var index in shownHumans)
        {
            var portrait = SetupPlayerCardArtLayout.PortraitDestination(index);
            if (_uiSprites is not null)
                batch.Draw(_uiSprites, portrait,
                    SetupPlayerCardArtLayout.PortraitSource(portraits[index]), Color.White);
            if (!_configuringOnlineLobby && index == _selectedSetupPlayerSlot)
            {
                if (_setupKeyedControls is not null)
                    batch.Draw(_setupKeyedControls,
                        new Rectangle(portrait.X, portrait.Y, 64, 62),
                        SetupPlayerCardArtLayout.ArrowOverlaySource, Color.White);
                else
                {
                    DrawHorizontalArrow(batch, pixel,
                        PlayerPortraitLayout.Previous(index), left: true, Color.Lime);
                    DrawHorizontalArrow(batch, pixel,
                        PlayerPortraitLayout.Next(index), left: false, Color.Lime);
                }
            }
            var label = _configuringOnlineLobby ? onlinePlayers[index].DisplayName : _editingPlayerName == index
                ? _setupNameEditor.Text + ((int)(_inputTime.TotalMilliseconds / 350) % 2 == 0 ? "_" : "")
                : _playerNames[index];
            var name = PlayerPortraitLayout.Name(index);
            font.Draw(batch, label, new Vector2(name.X, name.Y), PlayerColors[index], 1);
        }
        if (_setupPlayerDragStarted && _draggedSetupPlayerSlot is { } dragged
            && _uiSprites is not null)
        {
            var token = PlayerPortraitLayout.SetupDragToken(_dragPoint);
            batch.Draw(_uiSprites, token,
                OriginalSpriteLayout.OverlordPortrait(_playerPortraits[dragged]), Color.White);
            if (_uiKeyedSprites is not null)
                batch.Draw(_uiKeyedSprites, token,
                    OriginalSpriteLayout.SetupDragFrame, Color.White);
            if (HitTest.IndexAt(
                MatchLimits.PlayerCount, PlayerPortraitLayout.SetupHit, _dragPoint) is { } target
                && target >= 0)
                DrawBorder(batch, pixel, PlayerPortraitLayout.SetupLarge(target), Color.Lime, 2);
        }
        DrawSetupLight(batch, pixel,
            OriginalSelectionLightLayout.AiMentality((int)_selectedAiMentality));
        DrawSetupLight(batch, pixel,
            OriginalSelectionLightLayout.PlanningTime((int)_selectedPlanningTimeLimit));
        // FND-SETUP-013: while held, the pressed image covers the control, its light included.
        if (_setupControls is not null
            && _pressedSetupPanelControl is { } pressedControl
            && _hoverPoint is { } controlHover
            && SetupPanelLayout.Destination(pressedControl).Contains(controlHover))
            batch.Draw(_setupControls, SetupPanelLayout.Destination(pressedControl),
                SetupPanelLayout.PressedSource(pressedControl), Color.White);
        if (_configuringOnlineLobby)
        {
            // The seats belong to the lobby, so the two roster buttons and START are covered over
            // rather than left showing artwork that does nothing here.
            DrawButton(batch, pixel, font, SetupButtonLayout.AddPlayer, string.Empty, false);
            DrawButton(batch, pixel, font, SetupButtonLayout.RemovePlayer, string.Empty, false);
            DrawButton(batch, pixel, font, SetupButtonLayout.Start, string.Empty, false);
            DrawButton(batch, pixel, font, SetupButtonLayout.Back, "CONFIRM", true);
        }
        if (_message == ObjectiveDurationWarning)
            DrawHoverTooltip(batch, pixel, font, _hoverPoint ?? new Point(300, 280),
                ["TIME LIMIT DISABLED", "OBJECTIVE SCENARIOS RUN UNTIL THEIR GOAL IS MET."]);
        else if (_hoverPoint is { } hover && _pressedSetupPanelControl is null)
        {
            var control = SetupPanelLayout.HitTest(hover, timed: true);
            if (control is { Kind: SetupPanelControlKind.Scenario } scenario)
            {
                var lines = ScenarioSetupTooltip.Lines(
                    SetupScenarioButtons.ScenarioForButton(scenario.Index), _selectedDuration);
                DrawHoverTooltip(batch, pixel, font, hover, _configuringOnlineLobby
                    ? lines
                    : lines.Append(SetupRosterTooltip.ScenarioRemembered).ToArray());
            }
            else if (control is { Kind: SetupPanelControlKind.Duration } duration)
                DrawHoverTooltip(batch, pixel, font, hover,
                    DurationSetupTooltip.Lines(Durations[duration.Index]));
            else if (control is { Kind: SetupPanelControlKind.AiMentality } difficulty)
                DrawDifficultyTooltip(batch, pixel, font, (AiDifficulty)difficulty.Index);
            else if (!_configuringOnlineLobby && SetupButtonLayout.HitTest(hover) == SetupPushButton.AddPlayer)
                DrawHoverTooltip(batch, pixel, font, hover, SetupRosterTooltip.AddPlayer);
            else if (!_configuringOnlineLobby && _localSetupRoster.IsHuman(_selectedSetupPlayerSlot)
                     && (PlayerPortraitLayout.PreviousHit(_selectedSetupPlayerSlot).Contains(hover)
                         || PlayerPortraitLayout.NextHit(_selectedSetupPlayerSlot).Contains(hover)))
                DrawHoverTooltip(batch, pixel, font, hover, SetupRosterTooltip.PortraitArrow);
        }
    }

    private static void DrawSelectionLight(
        SpriteBatch batch, Texture2D pixel, Rectangle indicator)
    {
        batch.Draw(pixel, indicator, Color.Gold);
    }

    private static void DrawDifficultyTooltip(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        AiDifficulty difficulty)
    {
        var panel = new Rectangle(70, 210, 286, 104);
        batch.Draw(pixel, panel, new Color(8, 18, 16, 248));
        DrawBorder(batch, pixel, panel, Color.Lime, 2);
        var lines = DifficultyPresentation.Tooltip(difficulty)
            .Concat(["NO CASH, STAT, RNG, OR", "INFORMATION BONUSES."])
            .ToArray();
        for (var row = 0; row < lines.Length; row++)
            font.Draw(batch, lines[row], new Vector2(panel.X + 8, panel.Y + 8 + row * 12),
                row == 0 ? Color.Gold : Color.White, 1);
    }
}

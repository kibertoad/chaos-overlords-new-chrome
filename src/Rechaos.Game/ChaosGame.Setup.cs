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

    private void OpenNewGameSetup()
    {
        _screens.Show(ClientScreen.Setup);
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

    private readonly int _originalProcessSeed = DeterministicRandom.SeedFromTimerMilliseconds(
        unchecked((uint)Environment.TickCount));

    private void ChangeScenario(int delta)
    {
        var currentButton = SetupScenarioButtons.ButtonForScenario(_selectedScenario);
        _selectedScenario = SetupScenarioButtons.ScenarioForButton(
            Mod(currentButton + delta, SetupScenarioButtons.VisualOrder.Count));
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

    private void SelectSetupScenarioButton(int button)
    {
        var scenario = SetupScenarioButtons.ScenarioForButton(button);
        if (_selectedScenario != scenario) PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
        _selectedScenario = scenario;
        _message = string.Empty;
    }

    private void SelectSetupDurationButton(int button)
    {
        if (!ScenarioCatalog.Get(_selectedScenario).IsTimed)
        {
            RejectInput(ObjectiveDurationWarning);
            return;
        }
        if (_selectedDuration != Durations[button])
            PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
        _selectedDuration = Durations[button];
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
        if (_localSetupRoster.AddHuman() is not { } added) return false;
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
        _setupNameEditor.Begin(_playerNames[index]);
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
        var entered = _setupNameEditor.Text.Trim();
        _playerNames[index] = cancel
            ? _setupOriginalName
            : entered.Length == 0 ? LocalSetupPolicy.DefaultPlayerName(index) : entered;
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

    private void SelectDifficulty(AiDifficulty difficulty)
    {
        if (_selectedAiMentality != difficulty)
            PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
        _selectedAiMentality = difficulty;
        _message = string.Empty;
    }

    private void CyclePortrait(int player, int delta)
    {
        if (_configuringOnlineLobby) return;
        if (!_localSetupRoster.IsHuman(player)) return;
        _playerPortraits[player] = checked((short)Mod(
            _playerPortraits[player] + delta, PlayerPortraitLayout.SelectableCount));
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
        var target = Enumerable.Range(0, MatchLimits.PlayerCount)
            .FirstOrDefault(index => PlayerPortraitLayout.SetupHit(index).Contains(point), -1);
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
        var players = _localSetupRoster.HumanSlots.Order()
            .Select(slot => new MatchPlayerSetup(
                new PlayerId(slot), _playerNames[slot],
                PlayerController.Human,
                _playerPortraits[slot]))
            .ToArray();
        var setup = new MatchSetup(
            _selectedScenario, _selectedDuration, _originalProcessSeed, players,
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
        if (!_debugPhaseStepping) GameplayTurnFlow.AdvanceToPlanning(_actions.HotSeatRecorder);
        if (!_debugPhaseStepping) PrepareCurrentHireOffers();
        _cursor = _state.Players[0].Gangs[0].SectorId;
        _selectedGangIndex = 0;
        _message = string.Empty;
        _combatPresentationProgress.Clear();
        _combatAnimationPlayer.Clear();
        _siteSearchSelections.Reset();
        _lastTurnEventArchive.Clear();
        _managementReturnScreen = ClientScreen.City;
        _screens.Show(GameInformationPresentation.OpensAtNewGame(_state.Setup)
            ? ClientScreen.GameInfo
            : ClientScreen.City);
        StartPlanningTimer(_inputTime);
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
        DrawSelectionLight(batch, pixel, OriginalSelectionLightLayout.Scenario(
            SetupScenarioButtons.ButtonForScenario(_selectedScenario)));
        if (ScenarioCatalog.Get(_selectedScenario).IsTimed)
            DrawSelectionLight(batch, pixel, OriginalSelectionLightLayout.Duration(
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
            if (Enumerable.Range(0, MatchLimits.PlayerCount).FirstOrDefault(
                    index => PlayerPortraitLayout.SetupHit(index).Contains(_dragPoint), -1) is { } target
                && target >= 0)
                DrawBorder(batch, pixel, PlayerPortraitLayout.SetupLarge(target), Color.Lime, 2);
        }
        DrawSelectionLight(batch, pixel,
            OriginalSelectionLightLayout.AiMentality((int)_selectedAiMentality));
        DrawSelectionLight(batch, pixel,
            OriginalSelectionLightLayout.PlanningTime((int)_selectedPlanningTimeLimit));
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
        else if (_hoverPoint is { } hover)
        {
            var scenario = Array.FindIndex(SetupScenarios, rectangle => rectangle.Contains(hover));
            var duration = Array.FindIndex(SetupDurations, rectangle => rectangle.Contains(hover));
            var difficulty = Array.FindIndex(SetupAiMentalities, rectangle => rectangle.Contains(hover));
            if (scenario >= 0)
                DrawHoverTooltip(batch, pixel, font, hover,
                    ScenarioSetupTooltip.Lines(
                        SetupScenarioButtons.ScenarioForButton(scenario), _selectedDuration));
            else if (duration >= 0)
                DrawHoverTooltip(batch, pixel, font, hover,
                    DurationSetupTooltip.Lines(Durations[duration]));
            else if (difficulty >= 0)
                DrawDifficultyTooltip(batch, pixel, font, (AiDifficulty)difficulty);
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

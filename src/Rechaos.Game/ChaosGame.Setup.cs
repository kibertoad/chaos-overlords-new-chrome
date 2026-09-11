using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void ChangeScenario(int delta)
    {
        var count = ScenarioCatalog.All.Count;
        _selectedScenario = ScenarioCatalog.All[Mod((int)_selectedScenario + delta, count)].Id;
        PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
    }

    private void ChangeDuration(int delta)
    {
        _selectedDuration = Durations[Mod(Array.IndexOf(Durations, _selectedDuration) + delta, Durations.Length)];
        PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
    }

    private void ChangePlayerCount(int delta, bool pointerButton = false)
    {
        var changed = delta switch
        {
            > 0 => _localSetupRoster.AddHuman() is not null,
            < 0 => RemoveSetupHuman(),
            _ => false
        };
        if (AudioRouting.PlayerCountResultSound(
                changed, pointerButton) is { } slot)
            PlayGeneralSound(slot);
    }

    private bool RemoveSetupHuman()
    {
        if (_localSetupRoster.RemoveLastHuman() is not { } removed) return false;
        if (_editingPlayerName == removed) FinishSetupNameEdit(cancel: true);
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
            case SetupPushButton.AddPlayer:
                ChangePlayerCount(1, pointerButton: true);
                break;
            case SetupPushButton.RemovePlayer:
                ChangePlayerCount(-1, pointerButton: true);
                break;
            case SetupPushButton.Start:
                StartMatch();
                break;
            case SetupPushButton.Back:
                _screens.Show(ClientScreen.Title);
                break;
        }
    }

    private void BeginSetupNameEdit(int index)
    {
        if (!_localSetupRoster.IsHuman(index)) return;
        if (_editingPlayerName is not null) FinishSetupNameEdit(cancel: false);
        _editingPlayerName = index;
        _setupOriginalName = _playerNames[index];
        _setupNameEditor.Begin(_playerNames[index]);
        _message = "TYPE NAME  ENTER ACCEPTS  ESC CANCELS";
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
        _message = cancel ? "NAME CHANGE CANCELLED" : $"PLAYER {index + 1} NAME SET";
    }

    private void CycleDifficulty()
    {
        var values = Enum.GetValues<AiDifficulty>();
        _selectedAiMentality = values[Mod(
            Array.IndexOf(values, _selectedAiMentality) + 1, values.Length)];
        PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
        _message = $"AI MENTALITY {DifficultyPresentation.Label(_selectedAiMentality)}";
    }

    private void SelectDifficulty(AiDifficulty difficulty)
    {
        if (_selectedAiMentality != difficulty)
            PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
        _selectedAiMentality = difficulty;
        _message = $"AI MENTALITY {DifficultyPresentation.Label(difficulty)}";
    }

    private void CyclePortrait(int player, int delta)
    {
        if (!_localSetupRoster.IsHuman(player)) return;
        _playerPortraits[player] = checked((short)Mod(
            _playerPortraits[player] + delta, PlayerPortraitLayout.SelectableCount));
        PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
        _message = $"PLAYER {player + 1} PORTRAIT {_playerPortraits[player] + 1}";
    }

    private void BeginSetupPlayerDrag(int player, Point point)
    {
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
            .FirstOrDefault(index => PlayerPortraitLayout.SetupLarge(index).Contains(point), -1);
        var result = _localSetupRoster.MoveHuman(source, target);
        if (result is LocalSetupMoveResult.MovedToEmptyColor
            or LocalSetupMoveResult.ExchangedHumanColors)
        {
            (_playerNames[source], _playerNames[target]) =
                (_playerNames[target], _playerNames[source]);
            (_playerPortraits[source], _playerPortraits[target]) =
                (_playerPortraits[target], _playerPortraits[source]);
            PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
            _message = result == LocalSetupMoveResult.ExchangedHumanColors
                ? "PLAYER COLORS EXCHANGED"
                : $"PLAYER MOVED TO COLOR {target + 1}";
        }
        else if (result == LocalSetupMoveResult.Invalid)
        {
            PlayGeneralSound(GeneralSoundSlot.RejectedInput);
            _message = "DROP ON A PLAYER COLOR";
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
            _selectedScenario, _selectedDuration, Environment.TickCount, players,
            _selectedAiMentality, allowSparsePlayerIds: true);
        _diagnostics?.Write("match.started", new Dictionary<string, string?>
        {
            ["scenario"] = _selectedScenario.ToString(),
            ["duration"] = _selectedDuration.ToString(),
            ["configuredPlayers"] = _localSetupRoster.Count.ToString(),
            ["computerPlayers"] = "0",
            ["mentality"] = _selectedAiMentality.ToString(),
            ["seed"] = setup.InitialSeed.ToString()
        });
        _state = OriginalMatchFactory.Create(_definitions, setup);
        _actions = new MatchActions(new MatchReplayRecorder(_state));
        if (!_debugPhaseStepping) GameplayTurnFlow.AdvanceToPlanning(_actions.HotSeatRecorder);
        if (!_debugPhaseStepping) PrepareCurrentHireOffers();
        _cursor = _state.Players[0].Gangs[0].SectorId;
        _selectedGangIndex = 0;
        _message = _debugPhaseStepping ? "ADVANCE UPKEEP TO BEGIN" : "PLAN YOUR TURN";
        _combatPresentationProgress.Clear();
        _combatAnimationPlayer.Clear();
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
        DrawButton(batch, pixel, font, TitleQuit, "QUIT", true);
        DrawCentered(font, batch, "NEW CHROME", 280, new Color(210, 52, 43), 1);
        DrawCentered(font, batch, _message, 410, new Color(185, 195, 195), 1);
        DrawCentered(font, batch, "RESTORED BY KIBERTOAD", 430,
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
        DrawBorder(batch, pixel, SetupScenarios[(int)_selectedScenario], Color.Gold, 2);
        DrawBorder(batch, pixel, SetupDurations[Array.IndexOf(Durations, _selectedDuration)], Color.Gold, 2);
        for (var index = 0; index < MatchLimits.PlayerCount; index++)
            if (_uiSprites is not null)
                batch.Draw(_uiSprites, PlayerPortraitLayout.SetupTop(index),
                    OriginalSpriteLayout.OverlordPortrait(
                        _localSetupRoster.IsHuman(index)
                            ? _playerPortraits[index]
                            : PlayerPortraitLayout.Count - 1),
                    Color.White);
        font.Draw(batch, $"PLAYERS {_localSetupRoster.Count}", new Vector2(376, 306), Color.White, 1);
        foreach (var index in _localSetupRoster.HumanSlots)
        {
            var portrait = PlayerPortraitLayout.SetupLarge(index);
            if (_uiSprites is not null)
                batch.Draw(_uiSprites, portrait,
                    OriginalSpriteLayout.OverlordPortrait(_playerPortraits[index]), Color.White);
            DrawBorder(batch, pixel, portrait, PlayerColors[index], 1);
            DrawHorizontalArrow(batch, pixel, PlayerPortraitLayout.Previous(index), left: true, Color.Lime);
            DrawHorizontalArrow(batch, pixel, PlayerPortraitLayout.Next(index), left: false, Color.Lime);
            var label = _editingPlayerName == index
                ? _setupNameEditor.Text + ((int)(_inputTime.TotalMilliseconds / 350) % 2 == 0 ? "_" : "")
                : _playerNames[index];
            var name = PlayerPortraitLayout.Name(index);
            font.Draw(batch, label, new Vector2(name.X, name.Y), PlayerColors[index], 1);
        }
        if (_setupPlayerDragStarted && _draggedSetupPlayerSlot is { } dragged
            && _uiSprites is not null)
        {
            var token = new Rectangle(_dragPoint.X - 24, _dragPoint.Y - 24, 48, 48);
            batch.Draw(_uiSprites, token,
                OriginalSpriteLayout.OverlordPortrait(_playerPortraits[dragged]), Color.White);
            if (Enumerable.Range(0, MatchLimits.PlayerCount).FirstOrDefault(
                    index => PlayerPortraitLayout.SetupLarge(index).Contains(_dragPoint), -1) is { } target
                && target >= 0)
                DrawBorder(batch, pixel, PlayerPortraitLayout.SetupLarge(target), Color.Lime, 2);
        }
        DrawBorder(batch, pixel, SetupAiMentalities[(int)_selectedAiMentality], Color.Gold, 2);
        DrawBorder(batch, pixel,
            PlanningTimerLayout.SetupChoices[(int)_selectedPlanningTimeLimit], Color.Gold, 2);
        if (_hoverPoint is { } hover)
        {
            var hovered = Array.FindIndex(SetupAiMentalities, rectangle => rectangle.Contains(hover));
            if (hovered >= 0) DrawDifficultyTooltip(batch, pixel, font, (AiDifficulty)hovered);
        }
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

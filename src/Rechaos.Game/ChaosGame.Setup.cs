using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void ChangeScenario(int delta)
    {
        var count = ScenarioCatalog.All.Count;
        _selectedScenario = ScenarioCatalog.All[Mod((int)_selectedScenario + delta, count)].Id;
    }

    private void ChangeDuration(int delta)
    {
        _selectedDuration = Durations[Mod(Array.IndexOf(Durations, _selectedDuration) + delta, Durations.Length)];
    }

    private void ChangePlayerCount(int delta)
    {
        var previous = _selectedPlayerCount;
        _selectedPlayerCount = Math.Clamp(_selectedPlayerCount + delta, 1, MatchLimits.PlayerCount);
        for (var index = previous; index < _selectedPlayerCount; index++)
            _computerPlayers[index] = true;
    }

    private void ToggleController(int index)
    {
        if (index < 0 || index >= _selectedPlayerCount) return;
        _computerPlayers[index] = !_computerPlayers[index];
        _message = $"PLAYER {index + 1} {(_computerPlayers[index] ? "COMPUTER" : "HUMAN")}";
    }

    private void CycleDifficulty()
    {
        var values = Enum.GetValues<AiDifficulty>();
        _selectedAiMentality = values[Mod(
            Array.IndexOf(values, _selectedAiMentality) + 1, values.Length)];
        _message = $"AI MENTALITY {DifficultyPresentation.Label(_selectedAiMentality)}";
    }

    private void SelectDifficulty(AiDifficulty difficulty)
    {
        _selectedAiMentality = difficulty;
        _message = $"AI MENTALITY {DifficultyPresentation.Label(difficulty)}";
    }

    private void CyclePortrait(int player, int delta)
    {
        if (player < 0 || player >= _selectedPlayerCount) return;
        _playerPortraits[player] = checked((short)Mod(
            _playerPortraits[player] + delta, PlayerPortraitLayout.Count));
        _message = $"PLAYER {player + 1} PORTRAIT {_playerPortraits[player] + 1}";
    }

    private static Rectangle SetupPlayerSlot(int index) =>
        new(368 + index % 2 * 96, 120 + index / 2 * 64, 94, 54);

    private void StartMatch()
    {
        if (_definitions is null) return;
        var players = Enumerable.Range(0, _selectedPlayerCount)
            .Select(index => new MatchPlayerSetup(
                new PlayerId(index), $"PLAYER {index + 1}",
                _computerPlayers[index] ? PlayerController.Computer : PlayerController.Human,
                _playerPortraits[index]))
            .ToArray();
        var setup = new MatchSetup(
            _selectedScenario, _selectedDuration, Environment.TickCount, players, _selectedAiMentality);
        _diagnostics?.Write("match.started", new Dictionary<string, string?>
        {
            ["scenario"] = _selectedScenario.ToString(),
            ["duration"] = _selectedDuration.ToString(),
            ["configuredPlayers"] = _selectedPlayerCount.ToString(),
            ["computerPlayers"] = players.Count(player => player.Controller == PlayerController.Computer).ToString(),
            ["mentality"] = _selectedAiMentality.ToString(),
            ["seed"] = setup.InitialSeed.ToString()
        });
        _state = OriginalMatchFactory.Create(_definitions, setup);
        _actions = new MatchActions(new MatchReplayRecorder(_state));
        if (!_debugPhaseStepping) GameplayTurnFlow.AdvanceToPlanning(_actions.Replay);
        if (!_debugPhaseStepping) PrepareCurrentHireOffers();
        _cursor = _state.Players[0].Gangs[0].SectorId;
        _selectedGangIndex = 0;
        _message = _debugPhaseStepping ? "ADVANCE UPKEEP TO BEGIN" : "PLAN YOUR TURN";
        _lastAudibleEventSequence = -1;
        _lastAnimatedEventSequence = -1;
        _combatAnimationPlayer.Clear();
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
        DrawBorder(batch, pixel, SetupScenarios[(int)_selectedScenario], Color.Gold, 2);
        DrawBorder(batch, pixel, SetupDurations[Array.IndexOf(Durations, _selectedDuration)], Color.Gold, 2);
        for (var index = 0; index < MatchLimits.PlayerCount; index++)
            if (_uiSprites is not null)
                batch.Draw(_uiSprites, PlayerPortraitLayout.SetupTop(index),
                    OriginalSpriteLayout.OverlordPortrait(
                        index < _selectedPlayerCount ? _playerPortraits[index] : PlayerPortraitLayout.Count - 1),
                    Color.White);
        font.Draw(batch, $"PLAYERS {_selectedPlayerCount}", new Vector2(376, 306), Color.White, 1);
        for (var index = 0; index < _selectedPlayerCount; index++)
        {
            var portrait = PlayerPortraitLayout.SetupLarge(index);
            if (_uiSprites is not null)
                batch.Draw(_uiSprites, portrait,
                    OriginalSpriteLayout.OverlordPortrait(_playerPortraits[index]), Color.White);
            DrawBorder(batch, pixel, portrait, PlayerColors[index], 1);
            DrawHorizontalArrow(batch, pixel, PlayerPortraitLayout.Previous(index), left: true, Color.Lime);
            DrawHorizontalArrow(batch, pixel, PlayerPortraitLayout.Next(index), left: false, Color.Lime);
            var label = $"P{index + 1} {(_computerPlayers[index] ? "CPU" : "HUMAN")}";
            font.Draw(batch, label, new Vector2(portrait.X, portrait.Bottom + 2), PlayerColors[index], 1);
        }
        DrawBorder(batch, pixel, SetupAiMentalities[(int)_selectedAiMentality], Color.Gold, 2);
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

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Rechaos.Game;

public static class OptionsLayout
{
    public static Rectangle Panel => new(80, 24, 480, 412);
    public static Rectangle Done => new(264, 392, 112, 28);
    public static IReadOnlyList<Rectangle> MusicLevels { get; } =
        Enumerable.Range(0, OriginalSoundtrackPolicy.MaximumVolumeLevel + 1)
            .Select(level => new Rectangle(132 + level * 34, 78, 28, 28))
            .ToArray();
    public static IReadOnlyList<Rectangle> SoundEffectLevels { get; } =
        Enumerable.Range(0, AudioRouting.MaximumEffectVolumeLevel + 1)
            .Select(level => new Rectangle(132 + level * 34, 140, 28, 28))
            .ToArray();
    public static Rectangle BaseStatistics => new(150, 184, 340, 28);
    public static Rectangle DetailedCombat => new(150, 222, 340, 28);
    public static Rectangle SlidePanels => new(150, 260, 340, 28);
    public static Rectangle WarnIfIdleGangs => new(150, 298, 340, 28);
    public static Rectangle ColorDepth => new(150, 336, 340, 28);
}

public sealed partial class ChaosGame
{
    private ClientScreen _optionsReturnScreen = ClientScreen.Title;
    private string _optionsReturnMessage = string.Empty;
    private int _optionsRow;
    private int _soundEffectVolumeLevel = AudioRouting.DefaultEffectVolumeLevel;
    private bool _warnIfIdleGangs = OriginalOptionsPolicy.WarnIfIdleGangsByDefault;
    private bool _showBaseStatistics = OriginalOptionsPolicy.ShowBaseStatisticsByDefault;
    private bool _detailedCombat = OriginalOptionsPolicy.DetailedCombatByDefault;
    private bool _slidePanels = OriginalOptionsPolicy.SlidePanelsByDefault;
    private bool _fullscreen = OriginalOptionsPolicy.FullscreenByDefault;

    private void OpenOptions()
    {
        _optionsReturnScreen = _screens.Current;
        _optionsReturnMessage = _message;
        _optionsRow = 0;
        _screens.Show(ClientScreen.Options);
        _message = "UP/DOWN SELECTS; LEFT/RIGHT ADJUSTS";
    }

    private void CloseOptions()
    {
        _screens.Show(_optionsReturnScreen);
        _message = _optionsReturnMessage;
    }

    private void UpdateOptions(KeyboardState keyboard)
    {
        if (Pressed(keyboard, Keys.Up)) _optionsRow = Math.Max(0, _optionsRow - 1);
        if (Pressed(keyboard, Keys.Down)) _optionsRow = Math.Min(5, _optionsRow + 1);
        if (Pressed(keyboard, Keys.Left)) ChangeSelectedVolume(-1);
        if (Pressed(keyboard, Keys.Right)) ChangeSelectedVolume(1);
        if (_optionsRow >= 2 && Pressed(keyboard, Keys.Space)) ToggleSelectedOption();
        if (Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Back)
            || Pressed(keyboard, Keys.Escape) || Pressed(keyboard, Keys.O))
            CloseOptions();
    }

    private void HandleOptionsClick(Point point)
    {
        var level = Enumerable.Range(0, OptionsLayout.MusicLevels.Count)
            .FirstOrDefault(index => OptionsLayout.MusicLevels[index].Contains(point), -1);
        if (level >= 0)
        {
            _optionsRow = 0;
            SetMusicVolumeLevel(level);
            return;
        }
        level = Enumerable.Range(0, OptionsLayout.SoundEffectLevels.Count)
            .FirstOrDefault(index => OptionsLayout.SoundEffectLevels[index].Contains(point), -1);
        if (level >= 0)
        {
            _optionsRow = 1;
            SetSoundEffectVolumeLevel(level);
        }
        else if (OptionsLayout.BaseStatistics.Contains(point))
        {
            _optionsRow = 2;
            ToggleBaseStatistics();
        }
        else if (OptionsLayout.DetailedCombat.Contains(point))
        {
            _optionsRow = 3;
            ToggleDetailedCombat();
        }
        else if (OptionsLayout.SlidePanels.Contains(point))
        {
            _optionsRow = 4;
            ToggleSlidePanels();
        }
        else if (OptionsLayout.WarnIfIdleGangs.Contains(point))
        {
            _optionsRow = 5;
            ToggleIdleGangWarning();
        }
        else if (OptionsLayout.ColorDepth.Contains(point))
            _message = "THOUSANDS OF COLORS IS ALWAYS ENABLED";
        else if (OptionsLayout.Done.Contains(point)) CloseOptions();
    }

    private void ChangeSelectedVolume(int delta)
    {
        if (_optionsRow == 0)
            ChangeMusicVolume(delta);
        else if (_optionsRow == 1)
            SetSoundEffectVolumeLevel(Math.Clamp(
                _soundEffectVolumeLevel + delta,
                AudioRouting.MinimumEffectVolumeLevel,
                AudioRouting.MaximumEffectVolumeLevel));
        else ToggleSelectedOption();
    }

    private void ToggleSelectedOption()
    {
        if (_optionsRow == 2) ToggleBaseStatistics();
        else if (_optionsRow == 3) ToggleDetailedCombat();
        else if (_optionsRow == 4) ToggleSlidePanels();
        else if (_optionsRow == 5) ToggleIdleGangWarning();
    }

    private void ToggleBaseStatistics()
    {
        _showBaseStatistics = !_showBaseStatistics;
        SavePreferences();
        PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
        _message = $"GANG STATISTICS: {(_showBaseStatistics ? "BASE" : "CURRENT")}";
    }

    private void ToggleDetailedCombat()
    {
        _detailedCombat = !_detailedCombat;
        if (!_detailedCombat) _combatAnimationPlayer.Clear();
        SavePreferences();
        PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
        _message = $"DETAILED COMBAT {(_detailedCombat ? "ON" : "OFF")}";
    }

    private void ToggleSlidePanels()
    {
        _slidePanels = !_slidePanels;
        if (!_slidePanels) _panelSlideTransition.Clear();
        SavePreferences();
        PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
        _message = $"SLIDE PANELS {(_slidePanels ? "ON" : "OFF")}";
    }

    private void ChangeMusicVolume(int delta) =>
        SetMusicVolumeLevel(Math.Clamp(
            _musicVolumeLevel + delta,
            OriginalSoundtrackPolicy.MinimumVolumeLevel,
            OriginalSoundtrackPolicy.MaximumVolumeLevel));

    private void SetMusicVolumeLevel(int level)
    {
        var changed = level != _musicVolumeLevel;
        ApplyMusicVolumeLevel(level, _inputTime);
        SavePreferences();
        if (changed) PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
        _message = _musicVolumeLevel == 0
            ? "MUSIC OFF"
            : $"MUSIC LEVEL {_musicVolumeLevel}";
    }

    private void SetSoundEffectVolumeLevel(int level)
    {
        if (level is < AudioRouting.MinimumEffectVolumeLevel
            or > AudioRouting.MaximumEffectVolumeLevel)
            throw new ArgumentOutOfRangeException(nameof(level));
        var changed = level != _soundEffectVolumeLevel;
        _soundEffectVolumeLevel = level;
        SavePreferences();
        if (changed) PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
        _message = level == 0 ? "SOUND EFFECTS OFF" : $"SOUND EFFECTS LEVEL {level}";
    }

    private void ToggleIdleGangWarning()
    {
        _warnIfIdleGangs = !_warnIfIdleGangs;
        SavePreferences();
        PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
        _message = $"IDLE GANG WARNING {(_warnIfIdleGangs ? "ON" : "OFF")}";
    }

    private void SavePreferences() =>
        GamePreferencesStore.TrySave(
            _preferencesPath,
            new GamePreferences(
                GamePreferences.CurrentFormatVersion,
                _musicVolumeLevel,
                _soundEffectVolumeLevel,
                _warnIfIdleGangs,
                _selectedPlanningTimeLimit,
                _showBaseStatistics,
                _detailedCombat,
                _slidePanels,
                _fullscreen));

    private void ToggleFullscreen()
    {
        try
        {
            _graphics.ToggleFullScreen();
            _fullscreen = _graphics.IsFullScreen;
            SavePreferences();
            _message = _fullscreen ? "BORDERLESS FULLSCREEN" : "WINDOWED DISPLAY";
        }
        catch
        {
            _message = "DISPLAY MODE CHANGE FAILED";
        }
    }

    private void DrawOptions(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        var background = _optionsReturnScreen switch
        {
            ClientScreen.Title => _titleBackground,
            ClientScreen.Setup => _setupBackground,
            ClientScreen.Endgame => _endgameBackground,
            _ => _cityBackground
        };
        if (background is not null)
            batch.Draw(background, new Rectangle(0, 0, 640, 460), Color.White);
        batch.Draw(pixel, new Rectangle(0, 0, 640, 460), new Color(0, 0, 0, 190));
        batch.Draw(pixel, OptionsLayout.Panel, new Color(15, 25, 28, 245));
        DrawBorder(batch, pixel, OptionsLayout.Panel, new Color(80, 180, 130), 2);
        DrawCentered(font, batch, "OPTIONS", 38, Color.Gold, 2);
        DrawCentered(font, batch, "MUSIC VOLUME", 58,
            _optionsRow == 0 ? Color.Gold : Color.White, 1);

        DrawVolumeLevels(batch, pixel, font,
            OptionsLayout.MusicLevels, _musicVolumeLevel, _optionsRow == 0);
        DrawCentered(font, batch, "SOUND EFFECTS", 120,
            _optionsRow == 1 ? Color.Gold : Color.White, 1);
        DrawVolumeLevels(batch, pixel, font,
            OptionsLayout.SoundEffectLevels, _soundEffectVolumeLevel, _optionsRow == 1);

        DrawOptionToggle(batch, pixel, font, OptionsLayout.BaseStatistics,
            $"GANG STATISTICS: {(_showBaseStatistics ? "BASE" : "CURRENT")}", 2);
        DrawOptionToggle(batch, pixel, font, OptionsLayout.DetailedCombat,
            $"DETAILED COMBAT: {(_detailedCombat ? "ON" : "OFF")}", 3);
        DrawOptionToggle(batch, pixel, font, OptionsLayout.SlidePanels,
            $"SLIDE PANELS: {(_slidePanels ? "ON" : "OFF")}", 4);
        DrawOptionToggle(batch, pixel, font, OptionsLayout.WarnIfIdleGangs,
            $"WARN IF IDLE GANGS: {(_warnIfIdleGangs ? "ON" : "OFF")}", 5);
        DrawOptionToggle(batch, pixel, font, OptionsLayout.ColorDepth,
            "THOUSANDS OF COLORS: ALWAYS ON", -1);

        DrawCentered(font, batch, "F11 DISPLAY  UP/DOWN SELECTS  LEFT/RIGHT ADJUSTS", 374,
            new Color(185, 195, 195), 1);
        DrawButton(batch, pixel, font, OptionsLayout.Done, "DONE", true);
    }

    private void DrawOptionToggle(SpriteBatch batch, Texture2D pixel, PixelFont font,
        Rectangle rectangle, string label, int row)
    {
        var active = _optionsRow == row;
        batch.Draw(pixel, rectangle,
            active ? new Color(72, 54, 18, 245) : new Color(22, 40, 38, 245));
        DrawBorder(batch, pixel, rectangle,
            active ? Color.Gold : new Color(70, 110, 95), active ? 2 : 1);
        font.Draw(batch, label,
            new Vector2(rectangle.X + (rectangle.Width - label.Length * 6) / 2,
                rectangle.Y + 10), Color.White, 1);
    }

    private static void DrawVolumeLevels(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        IReadOnlyList<Rectangle> levels,
        int selectedLevel,
        bool active)
    {
        for (var level = 0; level < levels.Count; level++)
        {
            var rectangle = levels[level];
            batch.Draw(pixel, rectangle,
                level == selectedLevel ? new Color(72, 54, 18, 245) : new Color(22, 40, 38, 245));
            DrawBorder(batch, pixel, rectangle,
                level == selectedLevel && active ? Color.Gold : new Color(70, 110, 95),
                level == selectedLevel && active ? 2 : 1);
            var label = level == 0 ? "OFF" : level.ToString();
            font.Draw(batch, label,
                new Vector2(rectangle.X + (rectangle.Width - label.Length * 6) / 2, rectangle.Y + 12),
                Color.White, 1);
        }
    }
}

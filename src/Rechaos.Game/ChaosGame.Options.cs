using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Rechaos.Game;

public static class OptionsLayout
{
    public static Rectangle Panel => new(112, 70, 416, 318);
    public static Rectangle Done => new(264, 338, 112, 32);
    public static IReadOnlyList<Rectangle> MusicLevels { get; } =
        Enumerable.Range(0, OriginalSoundtrackPolicy.MaximumVolumeLevel + 1)
            .Select(level => new Rectangle(140 + level * 33, 158, 28, 32))
            .ToArray();
    public static IReadOnlyList<Rectangle> SoundEffectLevels { get; } =
        Enumerable.Range(0, AudioRouting.MaximumEffectVolumeLevel + 1)
            .Select(level => new Rectangle(140 + level * 33, 240, 28, 32))
            .ToArray();
    public static Rectangle WarnIfIdleGangs => new(190, 282, 260, 28);
}

public sealed partial class ChaosGame
{
    private ClientScreen _optionsReturnScreen = ClientScreen.Title;
    private string _optionsReturnMessage = string.Empty;
    private int _optionsRow;
    private int _soundEffectVolumeLevel = AudioRouting.DefaultEffectVolumeLevel;
    private bool _warnIfIdleGangs = true;

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
        if (Pressed(keyboard, Keys.Down)) _optionsRow = Math.Min(2, _optionsRow + 1);
        if (Pressed(keyboard, Keys.Left)) ChangeSelectedVolume(-1);
        if (Pressed(keyboard, Keys.Right)) ChangeSelectedVolume(1);
        if (_optionsRow == 2 && Pressed(keyboard, Keys.Space)) ToggleIdleGangWarning();
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
        else if (OptionsLayout.WarnIfIdleGangs.Contains(point))
        {
            _optionsRow = 2;
            ToggleIdleGangWarning();
        }
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
        else
            ToggleIdleGangWarning();
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
        if (changed) PlayGeneralSound(3);
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
        if (changed) PlayGeneralSound(3);
        _message = level == 0 ? "SOUND EFFECTS OFF" : $"SOUND EFFECTS LEVEL {level}";
    }

    private void ToggleIdleGangWarning()
    {
        _warnIfIdleGangs = !_warnIfIdleGangs;
        SavePreferences();
        PlayGeneralSound(3);
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
                _selectedPlanningTimeLimit));

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
        DrawCentered(font, batch, "OPTIONS", 88, Color.Gold, 2);
        DrawCentered(font, batch, "MUSIC VOLUME", 134,
            _optionsRow == 0 ? Color.Gold : Color.White, 1);

        DrawVolumeLevels(batch, pixel, font,
            OptionsLayout.MusicLevels, _musicVolumeLevel, _optionsRow == 0);
        DrawCentered(font, batch, "SOUND EFFECTS", 216,
            _optionsRow == 1 ? Color.Gold : Color.White, 1);
        DrawVolumeLevels(batch, pixel, font,
            OptionsLayout.SoundEffectLevels, _soundEffectVolumeLevel, _optionsRow == 1);

        batch.Draw(pixel, OptionsLayout.WarnIfIdleGangs,
            _optionsRow == 2 ? new Color(72, 54, 18, 245) : new Color(22, 40, 38, 245));
        DrawBorder(batch, pixel, OptionsLayout.WarnIfIdleGangs,
            _optionsRow == 2 ? Color.Gold : new Color(70, 110, 95),
            _optionsRow == 2 ? 2 : 1);
        DrawCentered(font, batch,
            $"WARN IF IDLE GANGS: {(_warnIfIdleGangs ? "ON" : "OFF")}", 292,
            Color.White, 1);

        DrawCentered(font, batch, "UP/DOWN SELECTS  LEFT/RIGHT ADJUSTS", 316,
            new Color(185, 195, 195), 1);
        DrawButton(batch, pixel, font, OptionsLayout.Done, "DONE", true);
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

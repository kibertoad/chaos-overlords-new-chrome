using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Rechaos.Game;

public static class OptionsLayout
{
    public static Rectangle Panel => new(112, 94, 416, 272);
    public static Rectangle Done => new(264, 314, 112, 32);
    public static IReadOnlyList<Rectangle> MusicLevels { get; } =
        Enumerable.Range(0, OriginalSoundtrackPolicy.MaximumVolumeLevel + 1)
            .Select(level => new Rectangle(140 + level * 33, 210, 28, 32))
            .ToArray();
}

public sealed partial class ChaosGame
{
    private ClientScreen _optionsReturnScreen = ClientScreen.Title;
    private string _optionsReturnMessage = string.Empty;

    private void OpenOptions()
    {
        _optionsReturnScreen = _screens.Current;
        _optionsReturnMessage = _message;
        _screens.Show(ClientScreen.Options);
        _message = "ADJUST MUSIC WITH LEFT/RIGHT";
    }

    private void CloseOptions()
    {
        _screens.Show(_optionsReturnScreen);
        _message = _optionsReturnMessage;
    }

    private void UpdateOptions(KeyboardState keyboard)
    {
        if (Pressed(keyboard, Keys.Left) || Pressed(keyboard, Keys.Down))
            ChangeMusicVolume(-1);
        if (Pressed(keyboard, Keys.Right) || Pressed(keyboard, Keys.Up))
            ChangeMusicVolume(1);
        if (Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Back)
            || Pressed(keyboard, Keys.Escape) || Pressed(keyboard, Keys.O))
            CloseOptions();
    }

    private void HandleOptionsClick(Point point)
    {
        var level = Enumerable.Range(0, OptionsLayout.MusicLevels.Count)
            .FirstOrDefault(index => OptionsLayout.MusicLevels[index].Contains(point), -1);
        if (level >= 0) SetMusicVolumeLevel(level);
        else if (OptionsLayout.Done.Contains(point)) CloseOptions();
    }

    private void ChangeMusicVolume(int delta) =>
        SetMusicVolumeLevel(Math.Clamp(
            _musicVolumeLevel + delta,
            OriginalSoundtrackPolicy.MinimumVolumeLevel,
            OriginalSoundtrackPolicy.MaximumVolumeLevel));

    private void SetMusicVolumeLevel(int level)
    {
        ApplyMusicVolumeLevel(level, _inputTime);
        GamePreferencesStore.TrySave(
            _preferencesPath,
            new GamePreferences(GamePreferences.CurrentFormatVersion, _musicVolumeLevel));
        _message = _musicVolumeLevel == 0
            ? "MUSIC OFF"
            : $"MUSIC LEVEL {_musicVolumeLevel}";
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
        DrawCentered(font, batch, "OPTIONS", 116, Color.Gold, 2);
        DrawCentered(font, batch, "MUSIC VOLUME", 166, Color.White, 1);

        for (var level = 0; level < OptionsLayout.MusicLevels.Count; level++)
        {
            var rectangle = OptionsLayout.MusicLevels[level];
            batch.Draw(pixel, rectangle,
                level == _musicVolumeLevel ? new Color(72, 54, 18, 245) : new Color(22, 40, 38, 245));
            DrawBorder(batch, pixel, rectangle,
                level == _musicVolumeLevel ? Color.Gold : new Color(70, 110, 95),
                level == _musicVolumeLevel ? 2 : 1);
            var label = level == 0 ? "OFF" : level.ToString();
            font.Draw(batch, label,
                new Vector2(rectangle.X + (rectangle.Width - label.Length * 6) / 2, rectangle.Y + 12),
                Color.White, 1);
        }

        DrawCentered(font, batch, "LEFT/RIGHT OR CLICK A LEVEL", 266,
            new Color(185, 195, 195), 1);
        DrawButton(batch, pixel, font, OptionsLayout.Done, "DONE", true);
    }
}

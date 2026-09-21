using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class OptionsLayout
{
    public static Rectangle Panel => new(80, 24, 480, 412);
    public static Rectangle Done => new(264, 400, 112, 28);
    public static Rectangle Music => new(120, 52, 400, 58);
    public static Rectangle SoundEffects => new(120, 114, 400, 58);
    public static IReadOnlyList<Rectangle> MusicLevels { get; } =
        Enumerable.Range(0, OriginalSoundtrackPolicy.MaximumVolumeLevel + 1)
            .Select(level => new Rectangle(132 + level * 34, 78, 28, 28))
            .ToArray();
    public static IReadOnlyList<Rectangle> SoundEffectLevels { get; } =
        Enumerable.Range(0, AudioRouting.MaximumEffectVolumeLevel + 1)
            .Select(level => new Rectangle(132 + level * 34, 140, 28, 28))
            .ToArray();
    public static Rectangle BaseStatistics => new(150, 174, 340, 26);
    public static Rectangle DetailedCombat => new(150, 202, 340, 26);
    public static Rectangle SlidePanels => new(150, 230, 340, 26);
    public static Rectangle WarnIfIdleGangs => new(150, 258, 340, 26);
    public static Rectangle EventSiteImages => new(150, 286, 340, 26);
    public static Rectangle AdvancedAi => new(150, 314, 340, 26);
    public static Rectangle ExportDiagnostics => new(150, 342, 340, 26);
    public static Rectangle ColorDepth => new(150, 370, 340, 16);
    public static Rectangle OnlineLobbyPresentation => ColorDepth;

    /// <summary>The first row below the two volume sliders.</summary>
    public const int FirstToggleRow = 2;

    /// <summary>The toggle rows in cursor order, starting at <see cref="FirstToggleRow"/>.</summary>
    public static IReadOnlyList<Rectangle> ToggleRows { get; } =
    [
        BaseStatistics, DetailedCombat, SlidePanels, WarnIfIdleGangs, EventSiteImages, AdvancedAi,
        ExportDiagnostics, OnlineLobbyPresentation
    ];
}

public static class OptionsTooltip
{
    public static IReadOnlyList<string> At(Point point)
    {
        if (OptionsLayout.Music.Contains(point))
            return ["MUSIC VOLUME", "SETS THE SOUNDTRACK LEVEL. OFF MUTES MUSIC."];
        if (OptionsLayout.SoundEffects.Contains(point))
            return ["SOUND EFFECTS", "SETS UI AND COMBAT SOUND LEVEL. OFF MUTES THEM."];
        if (OptionsLayout.BaseStatistics.Contains(point))
            return ["GANG STATISTICS", "BASE SHOWS PRINTED STATS; CURRENT INCLUDES MODIFIERS."];
        if (OptionsLayout.DetailedCombat.Contains(point))
            return ["DETAILED COMBAT", "ON PLAYS AUTOMATIC COMBAT ANIMATIONS."];
        if (OptionsLayout.SlidePanels.Contains(point))
            return ["SLIDE PANELS", "OFF MAKES SECTORS AND DETAILS APPEAR IMMEDIATELY."];
        if (OptionsLayout.WarnIfIdleGangs.Contains(point))
            return ["WARN IF IDLE GANGS", "ASKS BEFORE ENDING WITH UNASSIGNED ACTIVE GANGS."];
        if (OptionsLayout.EventSiteImages.Contains(point))
            return [
                "EVENT SITE IMAGES",
                "ORIGINAL USES THE NATIVE STRETCH AND ORDERED DITHER.",
                "SMOOTH USES LINEAR FILTERING FOR A CLEANER ENLARGEMENT."
            ];
        if (OptionsLayout.AdvancedAi.Contains(point))
            return [
                "ADVANCED AI",
                "ADDS USEFUL FALLBACK COMMANDS FOR GANGS ORIGINAL AI LEAVES IDLE.",
                "AT HIGHER DIFFICULTIES, HEALTHY GANGS MAY ALSO EXPAND.",
                "SETS THE INITIAL AI POLICY FOR FUTURE NEW MATCHES.",
                "IT DOES NOT CHANGE A MATCH ALREADY IN PROGRESS OR A LOADED SAVE."
            ];
        if (OptionsLayout.ExportDiagnostics.Contains(point))
            return [
                "EXPORT DIAGNOSTICS",
                "SAVES RECENT APP EVENTS AND CRASH SUMMARIES TO A LOCAL ZIP.",
                "USE IT FOR STARTUP, DISPLAY, AUDIO, OR CRASH PROBLEMS.",
                "IT SENDS NOTHING AND DOES NOT INCLUDE REPLAYABLE MATCH STATE.",
                "REPORT BUG SENDS YOUR DESCRIPTION AND OPTIONAL ANONYMIZED REPLAY.",
                "DIAGNOSTICS HELPS WITH CLIENT PROBLEMS A MATCH REPLAY CANNOT SHOW.",
                "NAMES, COMMANDS, SAVES, MESSAGES, AND FILE PATHS ARE OMITTED."
            ];
        if (OptionsLayout.ColorDepth.Contains(point))
            return [
                "ONLINE LOBBY APPEARANCE",
                "MODERN USES THE NEW MULTIPLAYER LAYOUT.",
                "CLASSIC USES THE ORIGINAL HOST-LOBBY ART WITH MODERN CONTROLS.",
                "THIS DOES NOT CHANGE THE SERVER, SESSION, OR JOIN KEY."
            ];
        if (OptionsLayout.Done.Contains(point))
            return ["DONE", "RETURNS TO THE GAME; CHANGES ARE SAVED IMMEDIATELY."];
        return [];
    }

    public static Rectangle Bounds(Point point, IReadOnlyList<string> lines)
        => HoverTooltipLayout.Bounds(point, lines);
}

public sealed partial class ChaosGame
{
    private ClientScreen _optionsReturnScreen = ClientScreen.Title;
    private bool _optionsReturnToGameMenu;
    private string _optionsReturnMessage = string.Empty;
    private int _optionsRow;
    private int _soundEffectVolumeLevel = AudioRouting.DefaultEffectVolumeLevel;
    private bool _warnIfIdleGangs = OriginalOptionsPolicy.WarnIfIdleGangsByDefault;
    private bool _showBaseStatistics = OriginalOptionsPolicy.ShowBaseStatisticsByDefault;
    private bool _detailedCombat = OriginalOptionsPolicy.DetailedCombatByDefault;
    private bool _slidePanels = OriginalOptionsPolicy.SlidePanelsByDefault;
    private bool _fullscreen = OriginalOptionsPolicy.FullscreenByDefault;
    private bool _smoothEventSiteImages = OriginalOptionsPolicy.SmoothEventSiteImagesByDefault;
    private OnlineLobbyPresentation _onlineLobbyPresentation = OnlineLobbyPresentation.Modern;
    private string _optionsStatus = string.Empty;

    private void OpenOptions()
    {
        _optionsReturnToGameMenu = false;
        _optionsReturnScreen = _screens.Current;
        _optionsReturnMessage = _message;
        _optionsRow = 0;
        _optionsStatus = string.Empty;
        _screens.Show(ClientScreen.Options);
        _message = string.Empty;
    }

    private void OpenOptionsFromGameMenu()
    {
        _gameMenuOpen = false;
        OpenOptions();
        _optionsReturnToGameMenu = true;
    }

    private void CloseOptions()
    {
        _screens.Show(_optionsReturnScreen);
        _message = _optionsReturnMessage;
        if (_optionsReturnToGameMenu) _gameMenuOpen = true;
        _optionsReturnToGameMenu = false;
    }

    private void UpdateOptions(KeyboardState keyboard)
    {
        if (Pressed(keyboard, Keys.Up)) _optionsRow = Math.Max(0, _optionsRow - 1);
        if (Pressed(keyboard, Keys.Down)) _optionsRow = Math.Min(9, _optionsRow + 1);
        if (Pressed(keyboard, Keys.Left)) ChangeSelectedVolume(-1);
        if (Pressed(keyboard, Keys.Right)) ChangeSelectedVolume(1);
        if (_optionsRow >= 2 && Pressed(keyboard, Keys.Space)) ToggleSelectedOption();
        if (Pressed(keyboard, Keys.Enter) && _optionsRow == 8)
            ExportDiagnostics();
        else if (Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Back)
                 || Pressed(keyboard, Keys.Escape) || Pressed(keyboard, Keys.O))
            CloseOptions();
    }

    private void HandleOptionsClick(Point point)
    {
        var level = HitTest.IndexAt(
            OptionsLayout.MusicLevels.Count, index => OptionsLayout.MusicLevels[index], point);
        if (level >= 0)
        {
            _optionsRow = 0;
            SetMusicVolumeLevel(level);
            return;
        }
        level = HitTest.IndexAt(
            OptionsLayout.SoundEffectLevels.Count, index => OptionsLayout.SoundEffectLevels[index], point);
        if (level >= 0)
        {
            _optionsRow = 1;
            SetSoundEffectVolumeLevel(level);
            return;
        }
        var toggle = HitTest.IndexAt(
            OptionsLayout.ToggleRows.Count, index => OptionsLayout.ToggleRows[index], point);
        if (toggle >= 0)
        {
            _optionsRow = OptionsLayout.FirstToggleRow + toggle;
            ToggleSelectedOption();
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
        else if (_optionsRow <= 9) ToggleSelectedOption();
    }

    private void ToggleSelectedOption()
    {
        if (_optionsRow == 2) ToggleBaseStatistics();
        else if (_optionsRow == 3) ToggleDetailedCombat();
        else if (_optionsRow == 4) ToggleSlidePanels();
        else if (_optionsRow == 5) ToggleIdleGangWarning();
        else if (_optionsRow == 6) ToggleEventSiteImageFilter();
        else if (_optionsRow == 7) ToggleAdvancedAi();
        else if (_optionsRow == 8) ExportDiagnostics();
        else if (_optionsRow == 9) ToggleOnlineLobbyPresentation();
    }

    private void ToggleBaseStatistics()
    {
        _showBaseStatistics = !_showBaseStatistics;
        SavePreferences();
        PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
        _message = string.Empty;
    }

    private void ToggleDetailedCombat()
    {
        _detailedCombat = !_detailedCombat;
        if (!_detailedCombat) _combatAnimationPlayer.Clear();
        SavePreferences();
        PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
        _message = string.Empty;
    }

    private void ToggleSlidePanels()
    {
        _slidePanels = !_slidePanels;
        if (!_slidePanels) _panelSlideTransition.Clear();
        SavePreferences();
        PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
        _message = string.Empty;
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
        _message = string.Empty;
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
        _message = string.Empty;
    }

    private void ToggleIdleGangWarning()
    {
        _warnIfIdleGangs = !_warnIfIdleGangs;
        SavePreferences();
        PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
        _message = string.Empty;
    }

    private void ToggleEventSiteImageFilter()
    {
        _smoothEventSiteImages = !_smoothEventSiteImages;
        SavePreferences();
        PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
        _message = string.Empty;
    }

    private void ToggleAdvancedAi()
    {
        _defaultAiPolicy = _defaultAiPolicy == AiPolicyMode.Original
            ? AiPolicyMode.Advanced
            : AiPolicyMode.Original;
        SavePreferences();
        PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
        _message = string.Empty;
        _optionsStatus = _state is null
            ? string.Empty
            : "APPLIES TO THE NEXT NEW GAME ONLY";
    }

    private void ToggleOnlineLobbyPresentation()
    {
        _onlineLobbyPresentation = _onlineLobbyPresentation == OnlineLobbyPresentation.Modern
            ? OnlineLobbyPresentation.Classic
            : OnlineLobbyPresentation.Modern;
        SavePreferences();
        PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
        _optionsStatus = "APPLIES WHEN YOU OPEN THE LOBBY";
    }

    private void ExportDiagnostics()
    {
        var result = _diagnostics?.Export();
        _optionsStatus = result?.Succeeded == true
            ? "SAVED TO APP DATA/DIAGNOSTICS"
            : "DIAGNOSTICS EXPORT FAILED";
        PlayGeneralSound(result?.Succeeded == true
            ? GeneralSoundSlot.AcceptedSelection
            : GeneralSoundSlot.RejectedInput);
    }

    /// <summary>
    /// Writes the player's own preferences.
    /// </summary>
    /// <remarks>
    /// Two of these are borrowed by a lobby, which reads the host's settings into the same fields
    /// the setup screen edits. What the player chose is what gets stored, so a lobby with a
    /// different timer or AI default cannot rewrite their preferences while they are in it.
    /// </remarks>
    private void SavePreferences() =>
        GamePreferencesStore.TrySave(
            _preferencesPath,
            new GamePreferences(
                GamePreferences.CurrentFormatVersion,
                _musicVolumeLevel,
                _soundEffectVolumeLevel,
                _warnIfIdleGangs,
                _localSetupBeforeLobby?.PlanningTimeLimit ?? _selectedPlanningTimeLimit,
                _showBaseStatistics,
                _detailedCombat,
                _slidePanels,
                _fullscreen,
                _smoothEventSiteImages,
                _introMoviesSeen,
                _localSetupBeforeLobby?.AiPolicy ?? _defaultAiPolicy,
                _online.Service,
                _online.Server.Value,
                _onlineLobbyPresentation));

    private void ToggleFullscreen()
    {
        try
        {
            _graphics.ToggleFullScreen();
            _fullscreen = _graphics.IsFullScreen;
            SavePreferences();
            _message = string.Empty;
        }
        catch
        {
            _message = "DISPLAY MODE CHANGE FAILED";
        }
    }

    private Texture2D? ReturnScreenBackground(ClientScreen returnScreen) => returnScreen switch
    {
        ClientScreen.Title => _titleBackground,
        ClientScreen.Setup => _setupBackground,
        ClientScreen.Endgame => _endgameBackground,
        _ => _cityBackground
    };

    private void DrawOptions(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        var background = ReturnScreenBackground(_optionsReturnScreen);
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
        DrawOptionToggle(batch, pixel, font, OptionsLayout.EventSiteImages,
            $"EVENT SITE IMAGES: {(_smoothEventSiteImages ? "SMOOTH" : "ORIGINAL")}", 6);
        DrawOptionToggle(batch, pixel, font, OptionsLayout.AdvancedAi,
            $"ADVANCED AI: {(_defaultAiPolicy == AiPolicyMode.Advanced ? "ON" : "OFF")}", 7);
        DrawOptionToggle(batch, pixel, font, OptionsLayout.ExportDiagnostics,
            "EXPORT DIAGNOSTICS", 8);
        DrawOptionToggle(batch, pixel, font, OptionsLayout.OnlineLobbyPresentation,
            $"ONLINE LOBBY: {(_onlineLobbyPresentation == OnlineLobbyPresentation.Classic ? "CLASSIC" : "MODERN")}", 9);

        DrawCentered(font, batch,
            string.IsNullOrEmpty(_optionsStatus)
                ? "F11 DISPLAY  UP/DOWN SELECTS  LEFT/RIGHT ADJUSTS"
                : _optionsStatus,
            388,
            new Color(185, 195, 195), 1);
        DrawButton(batch, pixel, font, OptionsLayout.Done, "DONE", true);
        if (_hoverPoint is { } hover)
            DrawHoverTooltip(batch, pixel, font, hover, OptionsTooltip.At(hover));
    }

    private static void DrawHoverTooltip(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        Point point,
        IReadOnlyList<string> lines,
        int? coloredSuffixRow = null,
        string? coloredSuffixPrefix = null,
        Color? suffixColor = null,
        int? coloredRow = null,
        Color? rowColor = null)
    {
        if (lines.Count == 0) return;
        var panel = HoverTooltipLayout.Bounds(point, lines);
        batch.Draw(pixel, panel, new Color(8, 18, 16, 252));
        DrawBorder(batch, pixel, panel, Color.Lime, 2);
        for (var row = 0; row < lines.Count; row++)
        {
            var position = new Vector2(panel.X + 8,
                panel.Y + 8 + row * OriginalFontLayout.LineHeight);
            var color = row == coloredRow && rowColor is { } requestedColor
                ? requestedColor
                : row == 0 ? Color.Gold : Color.White;
            if (row == coloredSuffixRow && suffixColor is { } highlight
                && coloredSuffixPrefix is { } prefix
                && lines[row].StartsWith(prefix, StringComparison.Ordinal))
            {
                font.Draw(batch, prefix, position, color, 1);
                font.Draw(batch, lines[row][prefix.Length..],
                    position + new Vector2(prefix.Length * OriginalFontLayout.CellWidth, 0), highlight, 1);
            }
            else font.Draw(batch, lines[row], position, color, 1);
        }
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

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.Assets;

namespace Rechaos.Game;

public static class HelpLayout
{
    public const int VisibleTopicRows = 15;
    public const int VisibleTextLines = 30;
    public const int TextColumns = 62;
    public static Rectangle Panel => new(18, 24, 604, 412);
    public static Rectangle TopicList => new(30, 64, 176, 318);
    public static Rectangle Text => new(218, 64, 392, 318);
    public static Rectangle Done => new(536, 394, 74, 28);
    public static Rectangle TopicRow(int row)
    {
        if (row is < 0 or >= VisibleTopicRows) throw new ArgumentOutOfRangeException(nameof(row));
        return new Rectangle(34, 72 + row * 20, 168, 18);
    }

    public static int TopicWindowStart(int topicCount, int selectedTopic)
    {
        if (topicCount <= 0) return 0;
        if (selectedTopic < 0 || selectedTopic >= topicCount)
            throw new ArgumentOutOfRangeException(nameof(selectedTopic));
        return Math.Clamp(selectedTopic - VisibleTopicRows / 2,
            0, Math.Max(0, topicCount - VisibleTopicRows));
    }

    public static int ScrollTopicWindow(int topicCount, int currentStart, int wheelDelta)
    {
        var maximum = Math.Max(0, topicCount - VisibleTopicRows);
        return Math.Clamp(currentStart - WheelSteps(wheelDelta), 0, maximum);
    }

    public static int WheelSteps(int wheelDelta)
    {
        if (wheelDelta == 0) return 0;
        var notches = Math.Max(1, Math.Abs(wheelDelta) / 120);
        return Math.Sign(wheelDelta) * notches;
    }
}

public static class HelpTextLayout
{
    public static IReadOnlyList<string> Wrap(string text, int columns)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r", "", StringComparison.Ordinal).Split('\n'))
        {
            if (paragraph.Length == 0)
            {
                lines.Add(string.Empty);
                continue;
            }
            var remaining = paragraph.Trim();
            while (remaining.Length > columns)
            {
                var split = remaining.LastIndexOf(' ', columns);
                if (split <= 0) split = columns;
                lines.Add(remaining[..split].TrimEnd());
                remaining = remaining[split..].TrimStart();
            }
            lines.Add(remaining);
        }
        return lines;
    }
}

public static class HelpNavigation
{
    public static IReadOnlyList<int> TopicOrder(ExtractedHelpDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var topics = document.Topics.ToList();
        var result = document.Contents
            .Where(entry => entry.TopicId is not null)
            .Select(entry => topics.FindIndex(topic => topic.Id == entry.TopicId!.Value))
            .Where(index => index >= 0)
            .Distinct()
            .ToArray();
        return result.Length > 0
            ? result
            : Enumerable.Range(0, document.Topics.Count).ToArray();
    }

    public static int FindTopicPosition(
        ExtractedHelpDocument document,
        IReadOnlyList<int> topicOrder,
        ClientScreen screen)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(topicOrder);
        var context = ContextReference(screen);
        var contextTopicId = document.Contents.FirstOrDefault(entry =>
            string.Equals(entry.ContextName, context, StringComparison.OrdinalIgnoreCase))?.TopicId;
        if (contextTopicId is { } exactTopicId)
        {
            var exactIndex = document.Topics.ToList().FindIndex(topic => topic.Id == exactTopicId);
            var exactPosition = PositionOf(topicOrder, exactIndex);
            if (exactPosition >= 0) return exactPosition;
        }
        var wanted = ContextTitle(screen);
        var topicIndex = document.Topics.ToList().FindIndex(topic =>
            string.Equals(NormalizeTitle(topic.Title), wanted,
                StringComparison.OrdinalIgnoreCase));
        var position = PositionOf(topicOrder, topicIndex);
        return position < 0 ? 0 : position;
    }

    public static int PositionOf(IReadOnlyList<int> topicOrder, int topicId)
    {
        ArgumentNullException.ThrowIfNull(topicOrder);
        for (var index = 0; index < topicOrder.Count; index++)
            if (topicOrder[index] == topicId) return index;
        return -1;
    }

    public static string ContextTitle(ClientScreen screen) => screen switch
    {
        ClientScreen.Setup => "Scenario Selection Control Panel",
        ClientScreen.Options => "Options Menu",
        ClientScreen.City => "City View",
        ClientScreen.Commands => "Commands",
        ClientScreen.Hire => "Hire",
        ClientScreen.Sector => "Sector View",
        ClientScreen.SectorGangs or ClientScreen.Gang => "Gang Information",
        ClientScreen.Site => "Sites",
        ClientScreen.ItemInformation or ClientScreen.Items => "Item Information",
        ClientScreen.Give or ClientScreen.GiveTarget => "Give",
        ClientScreen.Sell => "Sell",
        ClientScreen.GameInfo => "Game Info Screen",
        ClientScreen.ComlinkView or ClientScreen.ComlinkSend => "Comm Menu",
        ClientScreen.Finance => "The Inner Sanctum",
        ClientScreen.Ranking or ClientScreen.Endgame => "Endgame Screen",
        ClientScreen.Events or ClientScreen.CombatSummary or ClientScreen.Search =>
            "Main Control Panel",
        _ => "Introduction"
    };

    public static string ContextReference(ClientScreen screen) => screen switch
    {
        ClientScreen.Setup => "SSCP",
        ClientScreen.Options => "OPTMENU",
        ClientScreen.Online or ClientScreen.Lobby => "SETMPG",
        ClientScreen.City => "CITYVIEW",
        ClientScreen.Commands => "COMMAND",
        ClientScreen.Hire => "CONTHIRE",
        ClientScreen.Sector => "SECTVIEW",
        ClientScreen.SectorGangs or ClientScreen.Gang => "GANGINFO",
        ClientScreen.Site => "SITES",
        ClientScreen.ItemInformation or ClientScreen.Items => "ITEMINFO",
        ClientScreen.Give or ClientScreen.GiveTarget => "GIVE",
        ClientScreen.Sell => "SELL",
        ClientScreen.GameInfo => "GIS",
        ClientScreen.ComlinkView or ClientScreen.ComlinkSend => "COMMMENU",
        ClientScreen.Finance or ClientScreen.Handoff => "TIS",
        ClientScreen.Ranking or ClientScreen.Endgame => "ENDGAME2",
        ClientScreen.Events or ClientScreen.CombatSummary or ClientScreen.Search => "MCP",
        _ => "INTRO"
    };

    private static string NormalizeTitle(string title) => title
        .Replace("...", "", StringComparison.Ordinal)
        .Replace("…", "", StringComparison.Ordinal)
        .Trim();
}

public sealed partial class ChaosGame
{
    private ExtractedHelpDocument? _helpDocument;
    private ClientScreen _helpReturnScreen = ClientScreen.Title;
    private string _helpReturnMessage = string.Empty;
    private int _helpTopicIndex;
    private int _helpTopicOffset;
    private int _helpLineOffset;
    private IReadOnlyList<int> _helpTopicOrder = [];

    private void OpenHelp()
    {
        _helpReturnScreen = _screens.Current;
        _helpReturnMessage = _message;
        _helpTopicOrder = _helpDocument is null
            ? []
            : HelpNavigation.TopicOrder(_helpDocument);
        var topicPosition = _helpDocument is null
            ? 0
            : HelpNavigation.FindTopicPosition(
                _helpDocument, _helpTopicOrder, _helpReturnScreen);
        _helpTopicIndex = _helpTopicOrder.Count == 0
            ? 0
            : _helpTopicOrder[topicPosition];
        _helpTopicOffset = _helpDocument is null
            ? 0
            : HelpLayout.TopicWindowStart(_helpTopicOrder.Count, topicPosition);
        _helpLineOffset = 0;
        _screens.Show(ClientScreen.Help);
        _message = _helpDocument is null ? "HELP CONTENT IS UNAVAILABLE" : string.Empty;
    }

    private void CloseHelp()
    {
        _screens.Show(_helpReturnScreen);
        _message = _helpReturnMessage;
    }

    private void UpdateHelp(KeyboardState keyboard)
    {
        if (Pressed(keyboard, Keys.F1) || Pressed(keyboard, Keys.Escape)
            || Pressed(keyboard, Keys.Back))
        {
            CloseHelp();
            return;
        }
        if (Pressed(keyboard, Keys.Up)) ChangeHelpTopic(-1);
        if (Pressed(keyboard, Keys.Down)) ChangeHelpTopic(1);
        if (Pressed(keyboard, Keys.Home)) SelectHelpTopicPosition(0);
        if (Pressed(keyboard, Keys.End))
            SelectHelpTopicPosition(_helpTopicOrder.Count - 1);
        if (Pressed(keyboard, Keys.PageUp)) ScrollHelp(-HelpLayout.VisibleTextLines);
        if (Pressed(keyboard, Keys.PageDown) || Pressed(keyboard, Keys.Space))
            ScrollHelp(HelpLayout.VisibleTextLines);
    }

    private void ChangeHelpTopic(int delta)
    {
        if (_helpDocument is null || _helpTopicOrder.Count == 0) return;
        var current = HelpNavigation.PositionOf(_helpTopicOrder, _helpTopicIndex);
        SelectHelpTopicPosition(Math.Clamp(current + delta, 0, _helpTopicOrder.Count - 1));
    }

    private void SelectHelpTopicPosition(int position)
    {
        if (_helpDocument is null || position < 0 || position >= _helpTopicOrder.Count) return;
        _helpTopicIndex = _helpTopicOrder[position];
        if (position < _helpTopicOffset)
            _helpTopicOffset = position;
        else if (position >= _helpTopicOffset + HelpLayout.VisibleTopicRows)
            _helpTopicOffset = position - HelpLayout.VisibleTopicRows + 1;
        _helpLineOffset = 0;
    }

    private void ScrollHelp(int delta)
    {
        if (_helpDocument is null) return;
        var lines = HelpTextLayout.Wrap(
            _helpDocument.Topics[_helpTopicIndex].Text, HelpLayout.TextColumns);
        _helpLineOffset = Math.Clamp(_helpLineOffset + delta,
            0, Math.Max(0, lines.Count - HelpLayout.VisibleTextLines));
    }

    private void HandleHelpClick(Point point)
    {
        if (HelpLayout.Done.Contains(point)) CloseHelp();
        else if (_helpDocument is not null)
        {
            for (var row = 0; row < HelpLayout.VisibleTopicRows; row++)
                if (_helpTopicOffset + row < _helpTopicOrder.Count
                    && HelpLayout.TopicRow(row).Contains(point))
                {
                    SelectHelpTopicPosition(_helpTopicOffset + row);
                    break;
                }
        }
    }

    private void HandleHelpScroll(Point point, int wheelDelta)
    {
        if (_helpDocument is null || wheelDelta == 0) return;
        if (HelpLayout.TopicList.Contains(point))
            _helpTopicOffset = HelpLayout.ScrollTopicWindow(
                _helpTopicOrder.Count, _helpTopicOffset, wheelDelta);
        else if (HelpLayout.Text.Contains(point))
            ScrollHelp(-HelpLayout.WheelSteps(wheelDelta) * 3);
    }

    private void DrawHelp(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        var background = _helpReturnScreen switch
        {
            ClientScreen.Title => _titleBackground,
            ClientScreen.Setup => _setupBackground,
            ClientScreen.Endgame => _endgameBackground,
            _ => _cityBackground
        };
        if (background is not null)
            batch.Draw(background, new Rectangle(0, 0, 640, 460), Color.White);
        batch.Draw(pixel, new Rectangle(0, 0, 640, 460), new Color(0, 0, 0, 205));
        batch.Draw(pixel, HelpLayout.Panel, new Color(10, 23, 25, 250));
        DrawBorder(batch, pixel, HelpLayout.Panel, new Color(80, 180, 130), 2);
        DrawCentered(font, batch, _helpDocument?.Title.ToUpperInvariant() ?? "HELP UNAVAILABLE",
            40, Color.Gold, 1);

        if (_helpDocument is null)
        {
            DrawCentered(font, batch, "IMPORT THE ORIGINAL ASSETS AGAIN", 180, Color.White, 1);
            DrawCentered(font, batch, "TO CREATE THE MODERN HELP INDEX.", 198, Color.White, 1);
        }
        else
        {
            DrawHelpTopics(batch, pixel, font);
            DrawHelpText(batch, pixel, font);
        }
        DrawButton(batch, pixel, font, HelpLayout.Done, "DONE", true);
    }

    private void DrawHelpTopics(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        var topics = _helpDocument!.Topics;
        batch.Draw(pixel, HelpLayout.TopicList, new Color(18, 37, 38, 245));
        DrawBorder(batch, pixel, HelpLayout.TopicList, new Color(65, 105, 92), 1);
        for (var row = 0;
             row < HelpLayout.VisibleTopicRows && _helpTopicOffset + row < _helpTopicOrder.Count;
             row++)
        {
            var position = _helpTopicOffset + row;
            var index = _helpTopicOrder[position];
            var rectangle = HelpLayout.TopicRow(row);
            if (index == _helpTopicIndex)
                batch.Draw(pixel, rectangle, new Color(80, 58, 18, 245));
            var title = topics[index].Title.ToUpperInvariant();
            if (title.Length > 26) title = title[..26];
            font.Draw(batch, title, new Vector2(rectangle.X + 4, rectangle.Y + 5),
                index == _helpTopicIndex ? Color.Gold : Color.White, 1);
        }
    }

    private void DrawHelpText(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        var topic = _helpDocument!.Topics[_helpTopicIndex];
        batch.Draw(pixel, HelpLayout.Text, new Color(5, 14, 16, 245));
        DrawBorder(batch, pixel, HelpLayout.Text, new Color(65, 105, 92), 1);
        var title = topic.Title.ToUpperInvariant();
        if (title.Length > HelpLayout.TextColumns) title = title[..HelpLayout.TextColumns];
        font.Draw(batch, title, new Vector2(226, 74), Color.Gold, 1);
        var lines = HelpTextLayout.Wrap(topic.Text, HelpLayout.TextColumns);
        for (var row = 0; row < HelpLayout.VisibleTextLines && _helpLineOffset + row < lines.Count; row++)
            font.Draw(batch, lines[_helpLineOffset + row], new Vector2(226, 94 + row * 9),
                new Color(210, 220, 216), 1);
        font.Draw(batch,
            $"TOPIC {HelpNavigation.PositionOf(_helpTopicOrder, _helpTopicIndex) + 1}/{_helpTopicOrder.Count}  LINE {_helpLineOffset + 1}/{Math.Max(1, lines.Count)}",
            new Vector2(226, 370), new Color(155, 180, 172), 1);
    }
}

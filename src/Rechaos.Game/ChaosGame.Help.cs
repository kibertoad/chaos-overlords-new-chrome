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

    public static IReadOnlyList<HelpTextLine> Wrap(
        ExtractedHelpTopic topic,
        int columns)
    {
        ArgumentNullException.ThrowIfNull(topic);
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        var source = topic.Runs is { Count: > 0 }
            ? topic.Runs
            : [new ExtractedHelpTextRun(topic.Text)];
        var paragraphs = new List<List<HelpStyledCharacter>> { new() };
        foreach (var run in source)
        foreach (var character in run.Text)
        {
            if (character == '\r') continue;
            if (character == '\n') paragraphs.Add([]);
            else paragraphs[^1].Add(new HelpStyledCharacter(character, run));
        }

        var lines = new List<HelpTextLine>();
        foreach (var paragraph in paragraphs)
        {
            if (paragraph.Count == 0)
            {
                lines.Add(new HelpTextLine([]));
                continue;
            }
            var start = 0;
            while (paragraph.Count - start > columns)
            {
                var split = -1;
                for (var index = Math.Min(start + columns, paragraph.Count - 1);
                     index > start;
                     index--)
                    if (paragraph[index].Value == ' ')
                    {
                        split = index;
                        break;
                    }
                if (split < 0) split = start + columns;
                lines.Add(BuildLine(paragraph, start, split));
                start = split;
                while (start < paragraph.Count && paragraph[start].Value == ' ') start++;
            }
            lines.Add(BuildLine(paragraph, start, paragraph.Count));
        }
        return lines;
    }

    private static HelpTextLine BuildLine(
        IReadOnlyList<HelpStyledCharacter> paragraph,
        int start,
        int end)
    {
        while (end > start && paragraph[end - 1].Value == ' ') end--;
        var runs = new List<ExtractedHelpTextRun>();
        for (var index = start; index < end; index++)
        {
            var character = paragraph[index];
            var next = character.Style with { Text = character.Value.ToString() };
            if (runs.LastOrDefault() is { } previous && SameStyle(previous, next))
                runs[^1] = previous with { Text = previous.Text + next.Text };
            else
                runs.Add(next);
        }
        return new HelpTextLine(runs);
    }

    private static bool SameStyle(ExtractedHelpTextRun left, ExtractedHelpTextRun right) =>
        left.Bold == right.Bold && left.Italic == right.Italic
        && left.Underline == right.Underline && left.Strikethrough == right.Strikethrough
        && left.DoubleUnderline == right.DoubleUnderline && left.SmallCaps == right.SmallCaps
        && left.HalfPoints == right.HalfPoints && left.LinkHash == right.LinkHash
        && left.Popup == right.Popup;

    private readonly record struct HelpStyledCharacter(char Value, ExtractedHelpTextRun Style);
}

public sealed record HelpTextLine(IReadOnlyList<ExtractedHelpTextRun> Runs)
{
    public string Text => string.Concat(Runs.Select(run => run.Text));
}

public static class HelpContentAugmentation
{
    public const string NoteHeading = "NEW CHROME EXECUTABLE NOTE";

    private static readonly (string Context, string Title, string Text)[] Notes =
    [
        ("GSP", "Game Settings Panel", "AI Mentality changes resolution odds, not only planning. Humans always use the standard band. "
            + "Goon computers attack on 6, use 5+ for Heal, Influence, and Chaos, use 6 for Research, lose trunc(pool / 5) dice from Influence, Research, and Chaos, and lose trunc(Defense / 4) Defense when attacked. "
            + "Criminal computers use the human band: Attack, Heal, Influence, Chaos, and retaliation succeed on 5+, while Research succeeds on 6. "
            + "Crime Lord and Homicidal Maniac computers use the expert band: Attack, Heal, Influence, Chaos, and retaliation succeed on 4+, Research succeeds on 5+, and attacks against hidden gangs are 20 percentage points easier."),
        ("ATTACK", "Attack…", "For a human gang, attack dice = max(0, current Force + modified Combat - effective Defense). "
            + "Each 5 or 6 is a success. A positive pool causes damage equal to the greater of its successes and trunc(pool / 4). "
            + "Strength adds to bare-handed, melee, and blade attacks; Blade adds only to blade weapons; Range adds only to ranged weapons; Fighting and Martial Arts add only while bare-handed. "
            + "Eligible retaliation also succeeds on 5 or 6, then halves successes with truncation."),
        ("BRIBE", "Bribe", "The shipped game charges $3, not the manual's printed $5. Bribe directly adds 3 Tolerance and does not apply the printed 40-point cap."),
        ("CHAOS", "Chaos", "Each human gang separately rolls max(0, sector Income + Force + effective Chaos) dice at 5+. "
            + "Same-player successes in one sector are combined: controlled sectors pay the full total, uncontrolled sectors pay trunc(total / 2), and a present or newly triggered Crackdown prevents payment. Crackdown requires total sector Chaos to be strictly greater than Tolerance."),
        ("CONTROL", "Control", "Control uses no dice. Each player totals Force + effective Control from participating gangs, then subtracts sector Income. "
            + "Against an enemy sector it also subtracts Force + Control from every active non-hiding defender and Support from influenced sites. A positive margin captures; a single zero-margin challenger has a 50% chance because neutral/no-capture is an equal candidate."),
        ("EQUIP", "Equip…", "An influenced Factory in the acting gang's controlled sector changes price to Cost - trunc(Cost / 3). Replacing an occupied equipment slot destroys the old item."),
        ("HEAL", "Heal", "A human gang rolls max(0, 4 + effective Heal) dice. Only 5s and 6s restore Force, up to the maximum of 10."),
        ("HIDE", "Hide", "Against a human or standard computer attacker, chance to hit a hidden target is clamp((7 + attacker Detect - target Stealth) x 5, 0, 100)%. "
            + "An expert computer gets 20 additional percentage points. A successful hit against a hiding gang cannot be retaliated against. Hide does not by itself remove a gang from the sector display."),
        ("INFSITES", "Influence…", "Each human gang separately rolls max(0, Force + effective Influence) dice at 5+. "
            + "Successes immediately reduce remaining Resistance, so several queued gangs accumulate progress in roster order; once Resistance reaches zero, later gangs do not roll."),
        ("RESEARCH", "Research", "A human gang rolls max(0, Force + effective Research) dice. Only a 6 is a success. "
            + "Successes persist against the item's research requirement, and later same-turn gangs do not roll after the item is completed."),
        ("SELL", "Sell…", "Each sold item is removed and is worth trunc(Cost / 2). To match the shipped resolver, selling several slots in one command pays only the highest selected slot: miscellaneous, otherwise armor, otherwise weapon; the values are not added together."),
        ("SNITCH", "Snitch", "Snitch directly subtracts 3 Tolerance. After all Instant commands, every sector below 1 is raised to 1."),
        ("CRACKDOWN", "Crackdown", "Police detection chance (%) = clamp(115 - 5 x effective Stealth - (20 if the gang is Hiding), 0, 100). "
            + "Visible gangs are certain to be detected through Stealth 3 and cannot be detected at Stealth 23 or above; hiding gangs cannot be detected at Stealth 19 or above. "
            + "When detected, police roll max(0, Force 5 + Combat 20 - effective Defense) dice. Each 5 or 6 causes one Force of damage.")
    ];

    public static ExtractedHelpDocument AddExecutableNotes(ExtractedHelpDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var topics = document.Topics.ToList();
        var contents = document.Contents.ToList();
        var nextId = topics.Count == 0 ? 0 : checked(topics.Max(topic => topic.Id) + 1);
        var nextOffset = topics.Count == 0 ? 0 : checked(topics.Max(topic => topic.TopicOffset) + 1);
        var changed = false;
        foreach (var note in Notes)
        {
            var topicId = contents.FirstOrDefault(entry =>
                string.Equals(entry.ContextName, note.Context,
                    StringComparison.OrdinalIgnoreCase))?.TopicId;
            var topicIndex = topicId is null
                ? -1
                : topics.FindIndex(topic => topic.Id == topicId.Value);
            if (topicIndex < 0)
                topicIndex = topics.FindIndex(topic => string.Equals(
                    NormalizeSubject(topic.Title), NormalizeSubject(note.Title),
                    StringComparison.OrdinalIgnoreCase));
            if (topicIndex < 0)
            {
                topicIndex = topics.Count;
                topics.Add(new ExtractedHelpTopic(
                    nextId, note.Title, string.Empty, ListedInContents: true,
                    nextOffset, []));
                contents.Add(new ExtractedHelpContentsEntry(
                    1, note.Title, nextId, note.Context));
                nextId = checked(nextId + 1);
                nextOffset = checked(nextOffset + 1);
            }
            else if (!contents.Any(entry => entry.TopicId == topics[topicIndex].Id))
            {
                topics[topicIndex] = topics[topicIndex] with { ListedInContents = true };
                contents.Add(new ExtractedHelpContentsEntry(
                    1, note.Title, topics[topicIndex].Id, note.Context));
                changed = true;
            }
            if (topics[topicIndex].Text.Contains(
                    NoteHeading, StringComparison.Ordinal))
                continue;

            var topic = topics[topicIndex];
            IReadOnlyList<ExtractedHelpTextRun> runs = topic.Runs is { Count: > 0 }
                ? topic.Runs
                : topic.Text.Length > 0
                    ? [new ExtractedHelpTextRun(topic.Text)]
                    : [];
            var heading = topic.Text.Length == 0
                ? $"{NoteHeading}\n"
                : $"\n\n{NoteHeading}\n";
            topics[topicIndex] = topic with
            {
                Text = topic.Text + heading + note.Text,
                Runs = [.. runs,
                    new ExtractedHelpTextRun(heading, Bold: true),
                    new ExtractedHelpTextRun(note.Text)]
            };
            changed = true;
        }
        return changed ? document with { Topics = topics, Contents = contents } : document;
    }

    private static string NormalizeSubject(string title) => title
        .Replace("...", string.Empty, StringComparison.Ordinal)
        .Replace("…", string.Empty, StringComparison.Ordinal)
        .Trim();
}

public readonly record struct HelpLinkTarget(int TopicIndex, bool Popup);

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

    public static HelpLinkTarget? ResolveLink(
        ExtractedHelpDocument document,
        ExtractedHelpTextRun run)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(run);
        if (run.LinkHash is not { } hash || document.Contexts is null) return null;
        var context = document.Contexts.FirstOrDefault(entry => entry.Hash == hash);
        if (context is null) return null;
        var topicIndex = -1;
        var greatestOffset = -1;
        for (var index = 0; index < document.Topics.Count; index++)
        {
            var offset = document.Topics[index].TopicOffset;
            if (offset <= context.TargetOffset && offset > greatestOffset)
            {
                topicIndex = index;
                greatestOffset = offset;
            }
        }
        return topicIndex < 0 ? null : new HelpLinkTarget(topicIndex, run.Popup);
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
    private int? _helpPopupTopicIndex;
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
        _helpPopupTopicIndex = null;
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
            if (_helpPopupTopicIndex is not null)
            {
                _helpPopupTopicIndex = null;
                return;
            }
            CloseHelp();
            return;
        }
        if (_helpPopupTopicIndex is not null) return;
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
            _helpDocument.Topics[_helpTopicIndex], HelpLayout.TextColumns);
        _helpLineOffset = Math.Clamp(_helpLineOffset + delta,
            0, Math.Max(0, lines.Count - HelpLayout.VisibleTextLines));
    }

    private void HandleHelpClick(Point point)
    {
        if (_helpPopupTopicIndex is not null)
        {
            _helpPopupTopicIndex = null;
            return;
        }
        if (TryFollowHelpLink(point)) return;
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

    private bool TryFollowHelpLink(Point point)
    {
        if (_helpDocument is null || point.X < 226 || point.Y < 94
            || !HelpLayout.Text.Contains(point))
            return false;
        var lines = HelpTextLayout.Wrap(
            _helpDocument.Topics[_helpTopicIndex], HelpLayout.TextColumns);
        var row = (point.Y - 94) / OriginalFontLayout.LineHeight;
        if (row < 0 || row >= HelpLayout.VisibleTextLines
            || _helpLineOffset + row >= lines.Count)
            return false;
        var column = (point.X - 226) / OriginalFontLayout.CellWidth;
        var cursor = 0;
        foreach (var run in lines[_helpLineOffset + row].Runs)
        {
            if (column >= cursor && column < cursor + run.Text.Length
                && HelpNavigation.ResolveLink(_helpDocument, run) is { } target)
            {
                if (target.Popup)
                    _helpPopupTopicIndex = target.TopicIndex;
                else
                {
                    _helpTopicIndex = target.TopicIndex;
                    var position = HelpNavigation.PositionOf(_helpTopicOrder, target.TopicIndex);
                    if (position >= 0)
                        _helpTopicOffset = HelpLayout.TopicWindowStart(
                            _helpTopicOrder.Count, position);
                    _helpLineOffset = 0;
                }
                return true;
            }
            cursor += run.Text.Length;
        }
        return false;
    }

    private void HandleHelpScroll(Point point, int wheelDelta)
    {
        if (_helpDocument is null || _helpPopupTopicIndex is not null || wheelDelta == 0) return;
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
            if (_helpPopupTopicIndex is not null) DrawHelpPopup(batch, pixel, font);
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
        var lines = HelpTextLayout.Wrap(topic, HelpLayout.TextColumns);
        for (var row = 0; row < HelpLayout.VisibleTextLines && _helpLineOffset + row < lines.Count; row++)
            DrawHelpLine(batch, pixel, font, lines[_helpLineOffset + row], 226, 94 + row * 9);
        var position = HelpNavigation.PositionOf(_helpTopicOrder, _helpTopicIndex);
        var topicPosition = position < 0 ? "LINKED" : $"{position + 1}/{_helpTopicOrder.Count}";
        font.Draw(batch,
            $"TOPIC {topicPosition}  LINE {_helpLineOffset + 1}/{Math.Max(1, lines.Count)}",
            new Vector2(226, 370), new Color(155, 180, 172), 1);
    }

    private static void DrawHelpLine(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        HelpTextLine line,
        int x,
        int y)
    {
        foreach (var run in line.Runs)
        {
            var color = run.LinkHash is not null
                ? new Color(90, 220, 205)
                : run.HalfPoints >= 24 || run.Bold
                    ? Color.White
                    : run.Italic ? new Color(180, 205, 170) : new Color(210, 220, 216);
            font.Draw(batch, run.Text, new Vector2(x, y), color, 1);
            var width = run.Text.Length * OriginalFontLayout.CellWidth;
            if (run.Bold) font.Draw(batch, run.Text, new Vector2(x + 1, y), color, 1);
            if (run.Underline || run.LinkHash is not null)
                batch.Draw(pixel, new Rectangle(x, y + 7, width, 1), color);
            if (run.DoubleUnderline)
                batch.Draw(pixel, new Rectangle(x, y + 5, width, 1), color);
            if (run.Strikethrough)
                batch.Draw(pixel, new Rectangle(x, y + 3, width, 1), color);
            x += width;
        }
    }

    private void DrawHelpPopup(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        var topic = _helpDocument!.Topics[_helpPopupTopicIndex!.Value];
        var panel = new Rectangle(260, 116, 326, 210);
        batch.Draw(pixel, panel, new Color(8, 18, 20, 252));
        DrawBorder(batch, pixel, panel, new Color(90, 220, 205), 2);
        var hasAuthoredTitle = !topic.Title.StartsWith(
            "Additional topic ", StringComparison.Ordinal);
        if (hasAuthoredTitle)
        {
            var title = topic.Title.ToUpperInvariant();
            if (title.Length > 48) title = title[..48];
            font.Draw(batch, title, new Vector2(panel.X + 10, panel.Y + 10), Color.Gold, 1);
        }
        var lines = HelpTextLayout.Wrap(topic, 48);
        var firstLineY = panel.Y + (hasAuthoredTitle ? 28 : 10);
        var visibleLines = hasAuthoredTitle ? 18 : 20;
        for (var row = 0; row < visibleLines && row < lines.Count; row++)
            DrawHelpLine(batch, pixel, font, lines[row], panel.X + 10, firstLineY + row * 9);
    }
}

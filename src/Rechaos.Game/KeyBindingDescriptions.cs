using Microsoft.Xna.Framework.Input;

namespace Rechaos.Game;

/// <summary>What a shortcut does on one screen, or on a group of screens that read it alike.</summary>
public sealed record ShortcutUse(string Where, string Does)
{
    public override string ToString() => $"{Where}: {Does}";
}

/// <summary>What a shortcut does, as the Keys editor shows it (DEV-UI-024).</summary>
/// <param name="Summary">The row's text, true on every screen that reads the shortcut.</param>
/// <param name="Uses">Each screen that reads the shortcut and what it does there.</param>
public sealed record ShortcutDescription(string Summary, IReadOnlyList<ShortcutUse> Uses);

/// <summary>
/// The Keys editor's description of every shortcut in <see cref="KeyBindingMap.LogicalKeys"/>
/// (DEV-UI-024), and the names it gives keys.
/// </summary>
/// <remarks>
/// A shortcut is a key, and one key can mean different things on different screens, so each
/// description lists the screens that read it. Text fields read the keys as printed, so they are
/// left out. <c>KeyBindingTests</c> checks that every shortcut has a description and that the
/// screens' code reads no key the map does not list; a change to what a key does on a screen
/// changes its entry here.
/// </remarks>
public static class KeyBindingDescriptions
{
    private const string City = "CITY";
    private const string Lists = "LISTS AND MENUS";

    private static ShortcutDescription Describe(string summary, params (string Where, string Does)[] uses) =>
        new(summary, uses.Select(use => new ShortcutUse(use.Where, use.Does)).ToArray());

    private static readonly IReadOnlyDictionary<Keys, ShortcutDescription> Table =
        new Dictionary<Keys, ShortcutDescription>
        {
            [Keys.A] = Describe("LEFT, AWARDS, SELECT ALL",
                (City, "MOVE THE SELECTION LEFT"), ("ENDGAME", "AWARDS"), ("SITE SEARCH", "SELECT ALL")),
            [Keys.B] = Describe("COMBAT RESULTS", (City, "COMBAT RESULTS")),
            [Keys.Back] = Describe("GO BACK OR CLOSE",
                ("PANELS AND MENUS", "GO BACK OR CLOSE"), ("SECTOR", "BACK TO THE CITY")),
            [Keys.C] = Describe("ORDERS", (City, "ORDERS FOR THE SELECTED GANG")),
            [Keys.D] = Describe("RIGHT, REPLAY COMBAT",
                (City, "MOVE THE SELECTION RIGHT"), ("COMBAT SUMMARY", "REPLAY THE COMBAT")),
            [Keys.D1] = Describe("FIRST ITEM", ("GIVE AND SELL", "PICK OR DROP THE FIRST ITEM")),
            [Keys.D2] = Describe("SECOND ITEM", ("GIVE AND SELL", "PICK OR DROP THE SECOND ITEM")),
            [Keys.D3] = Describe("THIRD ITEM", ("GIVE AND SELL", "PICK OR DROP THE THIRD ITEM")),
            [Keys.Delete] = Describe("CLOSE EVENTS", ("EVENTS", "CLOSE")),
            [Keys.Down] = Describe("DOWN OR NEXT",
                ("CITY AND SECTOR", "MOVE THE SELECTION DOWN"), (Lists, "NEXT ENTRY"),
                ("ORDERS", "MOVE THE TARGET DOWN"), ("HELP", "NEXT TOPIC"),
                ("SETUP", "SHORTER GAME")),
            [Keys.E] = Describe("EQUIP", ("ITEMS", "EQUIP")),
            [Keys.End] = Describe("LAST HELP TOPIC", ("HELP", "LAST TOPIC")),
            [Keys.Enter] = Describe("CONFIRM, OPEN OR CLOSE",
                ("PANELS AND DIALOGS", "OK OR CLOSE"), (City, "OPEN THE SELECTED SECTOR"),
                ("SECTOR", "BACK TO THE CITY"), ("TITLE", "NEW GAME"), ("SETUP", "START"),
                ("ORDERS", "GIVE THE ORDER"), ("ENDGAME", "LEAVE"),
                ("HANDOFF, ELIMINATION, INTRO", "CONTINUE")),
            [Keys.Escape] = Describe("PAUSE MENU OR CANCEL",
                ("MATCH", "PAUSE MENU"), ("PICKERS AND DIALOGS", "CANCEL OR CLOSE"),
                ("COMBAT AND INTRO", "SKIP"), ("TITLE", "QUIT")),
            [Keys.Execute] = Describe("CONFIRM OR CLOSE",
                ("PANELS AND DIALOGS", "OK OR CLOSE"), (City, "OPEN THE SELECTED SECTOR"),
                ("SECTOR", "BACK TO THE CITY"), ("ORDERS", "GIVE THE ORDER")),
            [Keys.F] = Describe("FINANCES", (City, "FINANCES")),
            [Keys.F1] = Describe("HELP", ("ANYWHERE", "HELP, WITH SHIFT THE CREDITS"), ("HELP", "CLOSE")),
            [Keys.F5] = Describe("SAVE, REFRESH", (City, "SAVE THE GAME"), ("ONLINE MATCH LIST", "REFRESH")),
            [Keys.F6] = Describe("SAVE REPLAY", (City, "SAVE THE REPLAY")),
            [Keys.F9] = Describe("LOAD GAME", (City, "LOAD A GAME"), ("TITLE", "LOAD A GAME")),
            [Keys.F10] = Describe("LOAD REPLAY", (City, "LOAD A REPLAY")),
            [Keys.F11] = Describe("FULL SCREEN", ("ANYWHERE", "WINDOW OR FULL SCREEN")),
            [Keys.F12] = Describe("SCREENSHOT", ("ANYWHERE", "SAVE A SCREENSHOT")),
            [Keys.G] = Describe("NEXT OR PREVIOUS GANG", (City, "NEXT GANG"), ("ITEMS", "PREVIOUS GANG")),
            [Keys.H] = Describe("HIRE", (City, "HIRE")),
            [Keys.Home] = Describe("FIRST HELP TOPIC", ("HELP", "FIRST TOPIC")),
            [Keys.I] = Describe("SECTOR", (City, "OPEN THE SELECTED SECTOR")),
            [Keys.J] = Describe("SCENARIO INFORMATION", (City, "SCENARIO INFORMATION")),
            [Keys.K] = Describe("KEY BINDINGS", ("OPTIONS", "KEY BINDINGS")),
            [Keys.L] = Describe("PLANNING TIME LIMIT", ("SETUP", "PLANNING TIME LIMIT")),
            [Keys.Left] = Describe("LEFT OR PREVIOUS",
                ("CITY AND SECTOR", "MOVE THE SELECTION LEFT"), (Lists, "PREVIOUS ENTRY"),
                ("ORDERS", "MOVE THE TARGET LEFT"), ("ITEMS", "PREVIOUS GANG"),
                ("SETUP", "PREVIOUS SCENARIO"), ("OPTIONS", "LOWER THE VOLUME"),
                ("ONLINE", "PREVIOUS PORTRAIT")),
            [Keys.M] = Describe("READ COMLINK, DIFFICULTY", (City, "READ COMLINK"), ("SETUP", "DIFFICULTY")),
            [Keys.N] = Describe("SEND COMLINK, CLEAR", (City, "SEND COMLINK"), ("SITE SEARCH", "CLEAR")),
            [Keys.O] = Describe("OPTIONS", ("ANYWHERE", "OPTIONS"), ("OPTIONS", "CLOSE")),
            [Keys.OemMinus] = Describe("FEWER PLAYERS", ("SETUP", "FEWER PLAYERS")),
            [Keys.OemPlus] = Describe("MORE PLAYERS", ("SETUP", "MORE PLAYERS")),
            [Keys.PageDown] = Describe("HELP PAGE DOWN", ("HELP", "NEXT PAGE")),
            [Keys.PageUp] = Describe("HELP PAGE UP", ("HELP", "PREVIOUS PAGE")),
            [Keys.R] = Describe("RANKINGS, RESEARCH", (City, "PLAYER RANKINGS"), ("ITEMS", "RESEARCH")),
            [Keys.Right] = Describe("RIGHT OR NEXT",
                ("CITY AND SECTOR", "MOVE THE SELECTION RIGHT"), (Lists, "NEXT ENTRY"),
                ("ORDERS", "MOVE THE TARGET RIGHT"), ("ITEMS", "NEXT GANG"),
                ("SETUP", "NEXT SCENARIO"), ("OPTIONS", "RAISE THE VOLUME"),
                ("ONLINE", "NEXT PORTRAIT")),
            [Keys.S] = Describe("DOWN, STATS, SNUB, SELL",
                (City, "MOVE THE SELECTION DOWN"), ("ENDGAME", "STATISTICS"),
                ("HIRE", "SNUB THE OFFER"), ("ITEMS", "SELL")),
            [Keys.Space] = Describe("END TURN, TOGGLE, CONTINUE",
                (City, "END THE TURN"), ("OPTIONS, SEARCH, BUG REPORT", "TOGGLE THE SELECTED ENTRY"),
                ("HELP", "NEXT PAGE"), ("HANDOFF, ELIMINATION, INTRO", "CONTINUE")),
            [Keys.T] = Describe("ITEMS", (City, "ITEMS")),
            [Keys.Tab] = Describe("NEXT FIELD", ("ONLINE AND BUG REPORT", "NEXT FIELD")),
            [Keys.Up] = Describe("UP OR PREVIOUS",
                ("CITY AND SECTOR", "MOVE THE SELECTION UP"), (Lists, "PREVIOUS ENTRY"),
                ("ORDERS", "MOVE THE TARGET UP"), ("HELP", "PREVIOUS TOPIC"),
                ("SETUP", "LONGER GAME")),
            [Keys.V] = Describe("GIVE", ("ITEMS", "GIVE")),
            [Keys.W] = Describe("UP", (City, "MOVE THE SELECTION UP")),
            [Keys.X] = Describe("SITE SEARCH", (City, "SITE SEARCH")),
        };

    /// <summary>The description of <paramref name="logical"/>, or null for a key that is no shortcut.</summary>
    public static ShortcutDescription? Of(Keys logical) => Table.GetValueOrDefault(logical);

    /// <summary>
    /// The key's name in the editor, in the characters the game's font has. Letters and numbers
    /// print as themselves, the punctuation keys as their unshifted character where the font has
    /// it, and other keys by a short name.
    /// </summary>
    public static string KeyName(Keys key) => key switch
    {
        >= Keys.A and <= Keys.Z => ((char)('A' + (key - Keys.A))).ToString(),
        >= Keys.D0 and <= Keys.D9 => ((char)('0' + (key - Keys.D0))).ToString(),
        >= Keys.NumPad0 and <= Keys.NumPad9 => $"PAD {(char)('0' + (key - Keys.NumPad0))}",
        Keys.Back => "BACKSPACE",
        Keys.Enter => "ENTER",
        Keys.Escape => "ESC",
        Keys.Space => "SPACE",
        Keys.PageUp => "PAGE UP",
        Keys.PageDown => "PAGE DOWN",
        Keys.Delete => "DELETE",
        Keys.Insert => "INSERT",
        Keys.OemMinus => "-",
        Keys.OemPlus => "=",
        Keys.OemComma => ",",
        Keys.OemPeriod => ".",
        Keys.OemQuestion => "/",
        Keys.OemSemicolon => ";",
        Keys.OemQuotes => "'",
        Keys.OemOpenBrackets => "[",
        Keys.OemCloseBrackets => "]",
        Keys.OemPipe or Keys.OemBackslash => "BACKSLASH",
        Keys.OemTilde => "TILDE",
        Keys.Add => "PAD +",
        Keys.Subtract => "PAD -",
        Keys.Multiply => "PAD *",
        Keys.Divide => "PAD /",
        Keys.Decimal => "PAD .",
        _ => key.ToString().ToUpperInvariant()
    };
}

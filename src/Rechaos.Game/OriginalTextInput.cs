using Microsoft.Xna.Framework.Input;

namespace Rechaos.Game;

/// <summary>
/// The characters the original's two text entries take from a key, on a United States layout
/// (RULE-UI-014). Comlink text reads the key event of the window procedure; a setup name is typed
/// into a Windows edit control (FND-UI-064), which translates keys as Windows does.
/// </summary>
public static class OriginalTextInput
{
    // The United States layout's characters for Shift with the main digits 0 to 9.
    private const string ShiftedDigits = ")!@#$%^&*(";

    /// <summary>
    /// The character of the key event the window procedure stores (FND-UI-020, EXP-UI-052):
    /// <c>MapVirtualKeyA(key, 2)</c>, with Shift replacing the sixteen characters of the shift
    /// switch. Letters come out in upper case with or without Shift; Shift with a number-pad digit
    /// gives the shifted character of the main digit; the number-pad operators, minus and the
    /// keys outside the switch keep their character. Returns false for keys without a printable
    /// character.
    /// </summary>
    public static bool TryCharacter(Keys key, bool shift, out char character)
    {
        character = Translated(key);
        if (shift && character is >= '\'' and <= '=')
            character = character switch
            {
                '\'' => '"',
                ',' => '<',
                '.' => '>',
                '/' => '?',
                ';' => ':',
                '=' => '+',
                >= '0' and <= '9' => ShiftedDigits[character - '0'],
                _ => character,
            };
        return character != '\0';
    }

    /// <summary>
    /// The character the setup name editor's edit control types for a key (FND-UI-064,
    /// EXP-UI-053): the layout's character for the key with or without Shift, upper-cased by the
    /// control's <c>ES_UPPERCASE</c> style. Shift with a number-pad digit types nothing; Shift
    /// with a number-pad operator types the operator. Characters outside space to <c>Z</c> are
    /// returned as typed; the name copy turns them into spaces
    /// (<see cref="LocalSetupPolicy.NameAfterModalEntry"/>).
    /// </summary>
    public static bool TryNameCharacter(Keys key, bool shift, out char character)
    {
        character = Translated(key);
        if (shift)
        {
            if (key is >= Keys.NumPad0 and <= Keys.NumPad9) character = '\0';
            else if (key is >= Keys.D0 and <= Keys.D9) character = ShiftedDigits[key - Keys.D0];
            else
                character = key switch
                {
                    Keys.OemSemicolon => ':',
                    Keys.OemPlus => '+',
                    Keys.OemComma => '<',
                    Keys.OemMinus => '_',
                    Keys.OemPeriod => '>',
                    Keys.OemQuestion => '?',
                    Keys.OemTilde => '~',
                    Keys.OemOpenBrackets => '{',
                    Keys.OemPipe => '|',
                    Keys.OemCloseBrackets => '}',
                    Keys.OemQuotes => '"',
                    _ => character,
                };
        }
        return character != '\0';
    }

    // MapVirtualKeyA(key, 2) on a United States layout for the keys that give a printable
    // character (EXP-UI-052); every other key, the number-pad separator and the navigation keys
    // included, gives 0.
    private static char Translated(Keys key)
    {
        if (key is >= Keys.A and <= Keys.Z) return (char)('A' + (key - Keys.A));
        if (key is >= Keys.D0 and <= Keys.D9) return (char)('0' + (key - Keys.D0));
        if (key is >= Keys.NumPad0 and <= Keys.NumPad9) return (char)('0' + (key - Keys.NumPad0));
        return key switch
        {
            Keys.Space => ' ',
            Keys.Multiply => '*',
            Keys.Add => '+',
            Keys.Subtract => '-',
            Keys.Decimal => '.',
            Keys.Divide => '/',
            Keys.OemSemicolon => ';',
            Keys.OemPlus => '=',
            Keys.OemComma => ',',
            Keys.OemMinus => '-',
            Keys.OemPeriod => '.',
            Keys.OemQuestion => '/',
            Keys.OemTilde => '`',
            Keys.OemOpenBrackets => '[',
            Keys.OemPipe => '\\',
            Keys.OemCloseBrackets => ']',
            Keys.OemQuotes => '\'',
            _ => '\0',
        };
    }
}

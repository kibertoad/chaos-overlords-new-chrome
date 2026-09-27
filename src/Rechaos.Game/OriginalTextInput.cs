using Microsoft.Xna.Framework.Input;

namespace Rechaos.Game;

public static class OriginalTextInput
{
    public static bool TryCharacter(Keys key, bool shift, out char character)
    {
        if (key is >= Keys.A and <= Keys.Z)
        {
            character = (char)('A' + (int)key - (int)Keys.A);
            return true;
        }
        if (key is >= Keys.D0 and <= Keys.D9)
        {
            const string shifted = ")!@#$%^&*(";
            var index = (int)key - (int)Keys.D0;
            character = shift ? shifted[index] : (char)('0' + index);
            return true;
        }
        if (key is >= Keys.NumPad0 and <= Keys.NumPad9)
        {
            character = (char)('0' + (int)key - (int)Keys.NumPad0);
            return true;
        }
        character = key switch
        {
            Keys.Space => ' ',
            Keys.OemPeriod => shift ? '>' : '.',
            Keys.OemComma => shift ? '<' : ',',
            Keys.OemQuestion => shift ? '?' : '/',
            Keys.OemSemicolon => shift ? ':' : ';',
            Keys.OemQuotes => shift ? '"' : '\'',
            // The original's shift table covers the digits and ' , . / ; = only (RULE-UI-014), so
            // Shift with minus still gives the minus sign.
            Keys.OemMinus => '-',
            Keys.OemPlus => shift ? '+' : '=',
            _ => '\0'
        };
        return character != '\0';
    }
}

using Microsoft.Xna.Framework.Input;

namespace Rechaos.Game;

/// <summary>
/// SCR-SETUP-003: the text of the name dialog's single-line edit control, with its insertion
/// point and selection, shown on the player card (DEV-SETUP-003).
/// </summary>
/// <remarks>
/// FND-UI-068: the control has <c>ES_UPPERCASE</c> and <c>ES_AUTOHSCROLL</c> and the game sets
/// no text limit, so the control's default limit of 32,767 characters applies
/// (SRC-WIN32-EM-LIMITTEXT) and the text may run past the ten characters a name keeps; OK keeps
/// the first ten (<see cref="LocalSetupPolicy.NameAfterModalEntry"/>). The card shows a window of
/// <see cref="VisibleCharacters"/> characters that scrolls to keep the insertion point in view.
/// </remarks>
public sealed class SetupPlayerNameEditor
{
    /// <summary>SRC-WIN32-EM-LIMITTEXT: the limit an edit control has before EM_LIMITTEXT.</summary>
    public const int TextLimit = 32_767;

    /// <summary>DEV-SETUP-003: the card's name row holds ten six-pixel cells.</summary>
    public const int VisibleCharacters = LocalSetupPolicy.MaximumPlayerNameCharacters;

    private string _text = string.Empty;

    public string Text => _text;

    /// <summary>The insertion point, from 0 (before the first character) to the text's length.</summary>
    public int Caret { get; private set; }

    /// <summary>The end of the selection the insertion point does not sit at.</summary>
    public int Anchor { get; private set; }

    public int SelectionStart => Math.Min(Caret, Anchor);
    public int SelectionEnd => Math.Max(Caret, Anchor);
    public bool HasSelection => Caret != Anchor;

    /// <summary>The index of the first character the card shows.</summary>
    public int FirstVisible { get; private set; }

    /// <summary>The characters the card shows, from <see cref="FirstVisible"/>.</summary>
    public string VisibleText =>
        _text.Substring(FirstVisible, Math.Min(VisibleCharacters, _text.Length - FirstVisible));

    /// <summary>
    /// Starts the control with <paramref name="text"/>, all of it selected, as focusing a
    /// control does (SRC-WIN32-EDIT-TEXT). The setup editor starts empty, as the dialog's
    /// control does (FND-UI-068).
    /// </summary>
    public void Begin(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > TextLimit)
            throw new ArgumentException($"An edit control holds at most {TextLimit} characters.",
                nameof(text));
        _text = text;
        Anchor = 0;
        Caret = text.Length;
        FirstVisible = 0;
        ScrollToCaret();
    }

    /// <summary>
    /// A typed character replaces the selection, or goes in at the insertion point. ES_UPPERCASE
    /// turns a lower-case letter into a capital (SRC-WIN32-EDIT), accented Latin-1 letters
    /// included (EXP-UI-051). Control characters are not text, and nothing goes in once the text
    /// is at the limit.
    /// </summary>
    public bool Type(char character)
    {
        if (character < ' ' || character == (char)127) return false;
        character = UpperCase(character);
        if (_text.Length - (SelectionEnd - SelectionStart) >= TextLimit) return false;
        var at = SelectionStart;
        _text = string.Concat(_text.AsSpan(0, at), [character], _text.AsSpan(SelectionEnd));
        Collapse(at + 1);
        return true;
    }

    /// <summary>Deletes the selection, or the character before the insertion point.</summary>
    public bool Backspace()
    {
        if (HasSelection) return DeleteSelection();
        if (Caret == 0) return false;
        _text = _text.Remove(Caret - 1, 1);
        Collapse(Caret - 1);
        return true;
    }

    /// <summary>Deletes the selection, or the character after the insertion point.</summary>
    public bool Delete()
    {
        if (HasSelection) return DeleteSelection();
        if (Caret == _text.Length) return false;
        _text = _text.Remove(Caret, 1);
        Collapse(Caret);
        return true;
    }

    /// <summary>
    /// Moves the insertion point to <paramref name="index"/>. With <paramref name="extend"/> the
    /// selection runs from the anchor to the new point; without it the selection is cleared.
    /// </summary>
    public void MoveTo(int index, bool extend)
    {
        index = Math.Clamp(index, 0, _text.Length);
        Caret = index;
        if (!extend) Anchor = index;
        ScrollToCaret();
    }

    /// <summary>
    /// The editing keys of the control. Left and Right step the insertion point one character,
    /// Home and End move it to the start and the end of the text, Shift extends the selection
    /// instead of clearing it, and Backspace and Delete delete. Returns whether the key is one of
    /// them.
    /// </summary>
    public bool Press(Keys key, bool shift)
    {
        switch (key)
        {
            case Keys.Left:
                MoveTo(Caret - 1, shift);
                return true;
            case Keys.Right:
                MoveTo(Caret + 1, shift);
                return true;
            case Keys.Home:
                MoveTo(0, shift);
                return true;
            case Keys.End:
                MoveTo(_text.Length, shift);
                return true;
            case Keys.Back:
                Backspace();
                return true;
            case Keys.Delete:
                Delete();
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// The insertion point nearest to <paramref name="offset"/> pixels from the left edge of the
    /// shown text, each character taking <paramref name="cellWidth"/> pixels: a press in the left
    /// half of a character goes before it, one in the right half after it.
    /// </summary>
    public int IndexAt(int offset, int cellWidth)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(cellWidth, 1);
        var cells = (int)Math.Round(offset / (double)cellWidth, MidpointRounding.AwayFromZero);
        return Math.Clamp(FirstVisible + cells, FirstVisible, FirstVisible + VisibleText.Length);
    }

    // A letter whose capital lies outside Latin-1, such as y with a diaeresis, is kept as typed:
    // no run records what the control makes of it.
    private static char UpperCase(char character)
    {
        var capital = char.ToUpperInvariant(character);
        return capital <= (char)0xFF ? capital : character;
    }

    private bool DeleteSelection()
    {
        var at = SelectionStart;
        _text = _text.Remove(at, SelectionEnd - at);
        Collapse(at);
        return true;
    }

    private void Collapse(int index)
    {
        Caret = index;
        Anchor = index;
        ScrollToCaret();
    }

    /// <summary>
    /// ES_AUTOHSCROLL: the shown window moves as little as it must to keep the insertion point in
    /// view, and never so far that it shows fewer characters than it could.
    /// </summary>
    private void ScrollToCaret()
    {
        if (Caret < FirstVisible) FirstVisible = Caret;
        else if (Caret > FirstVisible + VisibleCharacters) FirstVisible = Caret - VisibleCharacters;
        FirstVisible = Math.Clamp(FirstVisible, 0, Math.Max(0, _text.Length - VisibleCharacters));
    }
}

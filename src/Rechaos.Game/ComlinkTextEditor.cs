using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// Original Comlink Send composition field: a fixed 4-by-40 character grid.
/// </summary>
/// <remarks>
/// Handler <c>0x0045eab1</c> stores and overwrites individual cells rather
/// than inserting into an append-only string. Its cursor wraps horizontally,
/// then clamps at the first and last row. The outgoing message trims only
/// trailing blank cells; interior blanks remain significant.
/// </remarks>
public sealed class ComlinkTextEditor
{
    private readonly char[] _cells = Enumerable.Repeat(' ', MatchLimits.ComlinkMessageCharacters).ToArray();

    public int Column { get; private set; }
    public int Row { get; private set; }
    public char CharacterAtCursor => _cells[CellIndex];
    public bool IsFull => _cells.All(character => character != ' ');

    /// <summary>
    /// Every cell is a space, the test RULE-COMLINK-003 makes before storing a message.
    /// </summary>
    public bool IsBlank => _cells.All(character => character == ' ');

    /// <summary>
    /// The draft with its trailing spaces removed. FMT-STATE-005 `text` is the 160 cells padded
    /// with spaces; padding this string back to 160 gives the same bytes, since a cell is never
    /// anything below a space (RULE-COMLINK-006).
    /// </summary>
    public string Text => new string(_cells).TrimEnd();

    public bool TryAppend(char character)
    {
        character = char.ToUpperInvariant(character);
        if (character is < OriginalFontLayout.FirstCharacter
            or > OriginalFontLayout.LastCharacter)
            return false;

        _cells[CellIndex] = character;
        MoveRight();
        return true;
    }

    public bool Backspace()
    {
        MoveLeft();
        _cells[CellIndex] = ' ';
        return true;
    }

    public void MoveLeft()
    {
        Column--;
        NormalizeCursor();
    }

    public void MoveUp()
    {
        Row--;
        NormalizeCursor();
    }

    public void MoveRight()
    {
        Column++;
        NormalizeCursor();
    }

    public void MoveDown()
    {
        Row++;
        NormalizeCursor();
    }

    public void MoveNextRow()
    {
        Column = 0;
        Row++;
        NormalizeCursor();
    }

    public void Clear()
    {
        Array.Fill(_cells, ' ');
        Column = 0;
        Row = 0;
    }

    public IReadOnlyList<string> DisplayLines() =>
        Enumerable.Range(0, ComlinkSendLayout.MessageRows)
            .Select(row => new string(_cells, row * ComlinkSendLayout.MessageColumns,
                ComlinkSendLayout.MessageColumns))
            .ToArray();

    public static IReadOnlyList<string> DisplayLines(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = new string[ComlinkSendLayout.MessageRows];
        for (var row = 0; row < lines.Length; row++)
        {
            var start = row * ComlinkSendLayout.MessageColumns;
            lines[row] = start >= text.Length
                ? string.Empty
                : text.Substring(start, Math.Min(ComlinkSendLayout.MessageColumns,
                    text.Length - start));
        }
        return lines;
    }

    private int CellIndex => Row * ComlinkSendLayout.MessageColumns + Column;

    private void NormalizeCursor()
    {
        if (Column < 0)
        {
            Column = ComlinkSendLayout.MessageColumns - 1;
            Row--;
        }
        else if (Column >= ComlinkSendLayout.MessageColumns)
        {
            Column = 0;
            Row++;
        }
        Row = Math.Clamp(Row, 0, ComlinkSendLayout.MessageRows - 1);
    }
}

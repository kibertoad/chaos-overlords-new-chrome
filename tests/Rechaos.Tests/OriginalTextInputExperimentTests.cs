using System.Globalization;
using System.Text.Json;
using Microsoft.Xna.Framework.Input;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// RULE-UI-014: the characters the original's text entries take from a key, on a United States
/// layout. EXP-UI-052 posted every number-pad key, the main keyboard's digits and punctuation and
/// the navigation keys the number pad gives with Num Lock off to the game window, with the
/// window procedure's Shift test reporting Shift held and not (FND-UI-064), and recorded the key
/// event the procedure stored; Comlink text reads that event (FND-UI-020). EXP-UI-053 typed the
/// same keys into the setup name editor's edit control and recorded the name the game kept after
/// OK (RULE-SETUP-009, FND-UI-022, FND-UI-064).
/// </summary>
public sealed class OriginalTextInputExperimentTests
{
    [Fact]
    public void KeyEventsCarryTheOriginalsCharacter()
    {
        var events = Run("EXP-UI-052").GetProperty("key_events").EnumerateArray().ToList();
        Assert.Equal(98, events.Count);
        foreach (var entry in events)
        {
            var values = entry.EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var (key, shift, type, character, storedKey) = ((Keys)values[0], values[1] == 1, values[2], values[3], values[4]);
            Assert.Equal(2, type);
            Assert.Equal((int)key, storedKey);
            var typed = OriginalTextInput.TryCharacter(key, shift, out var rebuilt);
            // A control character (Enter) or 0 is not text; the rebuild's screens handle Enter
            // as a key of their own.
            if (character < ' ')
                Assert.False(typed, $"{key} with Shift {shift} gave {character:X2}");
            else
            {
                Assert.True(typed, $"{key} with Shift {shift}");
                Assert.True(character == rebuilt, $"{key} with Shift {shift}: {character:X2}, rebuilt {(int)rebuilt:X2}");
            }
        }
    }

    [Fact]
    public void TheNameEditorKeepsTheOriginalsName()
    {
        var entries = Run("EXP-UI-053").GetProperty("name_entries").EnumerateArray().ToList();
        Assert.Equal(98, entries.Count);
        var compared = 0;
        foreach (var entry in entries)
        {
            var keys = entry.GetProperty("keys").GetString()!;
            // Shift with Insert pastes the clipboard into the edit control; the rebuild's editor
            // has no clipboard, so that entry is not compared.
            if (keys.Contains("{SHIFT}{VK2D}", StringComparison.Ordinal)) continue;
            var record = entry.GetProperty("name").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var expected = new string(record.Skip(1).Take(record[0]).Select(value => (char)value).ToArray());

            var editor = new SetupPlayerNameEditor();
            editor.Begin(string.Empty);
            var shift = false;
            foreach (var (kind, value) in Tokens(keys))
            {
                switch (kind)
                {
                    case "SHIFT": shift = true; break;
                    case "PLAIN": shift = false; break;
                    // A posted WM_CHAR reaches the control as typed.
                    case "CHAR": editor.Type((char)value); break;
                    case "VK":
                        if (OriginalTextInput.TryNameCharacter((Keys)value, shift, out var character))
                            editor.Type(character);
                        break;
                }
            }
            // OK keeps the first ten characters of the control's text, each outside space to Z as
            // a space (FND-UI-022).
            var kept = LocalSetupPolicy.NameAfterModalEntry(string.Empty, editor.Text);
            Assert.True(expected == kept, $"{keys}: \"{expected}\", rebuilt \"{kept}\"");
            compared++;
        }
        Assert.Equal(97, compared);
    }

    private static IEnumerable<(string Kind, int Value)> Tokens(string keys)
    {
        foreach (var token in keys.Split('{', StringSplitOptions.RemoveEmptyEntries).Select(part => part.TrimEnd('}')))
        {
            if (token is "SHIFT" or "PLAIN") yield return (token, 0);
            else if (token.StartsWith("CHAR", StringComparison.Ordinal))
                yield return ("CHAR", int.Parse(token[4..], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            else yield return ("VK", int.Parse(token[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }
    }

    private static JsonElement Run(string experiment)
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "spec", "experiments", $"{experiment}.json")));
        return fixture.RootElement.GetProperty("runs")[0].Clone();
    }
}

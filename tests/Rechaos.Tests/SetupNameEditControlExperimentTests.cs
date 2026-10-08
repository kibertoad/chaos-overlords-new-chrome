using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework.Input;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// SCR-SETUP-003 against EXP-UI-051: each name step of the run typed keys into the name dialog of
/// the original and read the edit control's text and selection at each <c>{SHOT}</c>, then closed
/// the dialog and read the slot's name record. The rebuild's editor takes the same keys and has to
/// hold the same text and selection at each shot, and the name it keeps has to be the record's.
/// </summary>
/// <remarks>
/// A press places the caret by the pixel widths of the dialog's font, and the rebuild's editor
/// places it by its own cells (DEV-SETUP-003), so after a <c>{PRESSxx}</c> the editor takes the
/// caret the next shot read instead of comparing it. The text is compared there as well.
/// </remarks>
public sealed partial class SetupNameEditControlExperimentTests
{
    private const string Experiment = "EXP-UI-051";

    [GeneratedRegex(@"\{(?<token>SHIFT|PLAIN|SHOT|CHAR|PRESS|VK)(?<value>[0-9A-F]*)\}")]
    private static partial Regex Token();

    private sealed record Shot(string Text, int Start, int End);

    [Fact]
    public void TheEditorHoldsWhatTheEditControlHeldAndKeepsTheSameName()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "spec", "experiments", $"{Experiment}.json")));
        var run = fixture.RootElement.GetProperty("runs")[0];
        var shots = run.GetProperty("name_shots").EnumerateArray()
            .GroupBy(shot => shot.GetProperty("entry").GetInt32())
            .ToDictionary(group => group.Key, group => group
                .OrderBy(shot => shot.GetProperty("shot").GetInt32())
                .Select(shot => new Shot(shot.GetProperty("text").GetString()!,
                    shot.GetProperty("selection")[0].GetInt32(), shot.GetProperty("selection")[1].GetInt32()))
                .ToArray());
        var entries = run.GetProperty("name_entries").EnumerateArray().ToArray();
        Assert.NotEmpty(entries);
        // The name before the first step is what that step kept, since it typed nothing.
        var name = Name(entries[0]);
        for (var entry = 0; entry < entries.Length; entry++)
        {
            var keys = entries[entry].GetProperty("keys").GetString()!;
            var kept = Replay(keys, name, shots.GetValueOrDefault(entry) ?? [], $"step {entry}");
            Assert.Equal(Name(entries[entry]), kept);
            name = kept;
        }
    }

    // Plays the tokens of one name step into a new editor and returns the name the dialog's close
    // leaves: OK at the end of the step or at Enter, the name unchanged at Escape.
    private static string Replay(string keys, string name, Shot[] shots, string step)
    {
        var editor = new SetupPlayerNameEditor();
        editor.Begin(string.Empty);
        var shift = false;
        var shot = 0;
        var pressed = false;
        foreach (Match match in Token().Matches(keys))
        {
            var value = match.Groups["value"].Value;
            switch (match.Groups["token"].Value)
            {
                case "SHIFT":
                    shift = true;
                    break;
                case "PLAIN":
                    shift = false;
                    break;
                case "CHAR":
                    // The control is an ANSI window; the characters posted are all from Windows-1252's
                    // Latin-1 range, where the code page and Unicode agree.
                    editor.Type((char)Convert.ToInt32(value, 16));
                    break;
                case "PRESS":
                    pressed = true;
                    break;
                case "SHOT":
                    var read = shots[shot++];
                    if (pressed) editor.MoveTo(read.End, extend: false);
                    pressed = false;
                    Assert.True(read.Text == editor.Text, $"{step} shot {shot - 1}: text {editor.Text}, the original's {read.Text}");
                    Assert.True((read.Start, read.End) == (editor.SelectionStart, editor.SelectionEnd),
                        $"{step} shot {shot - 1}: selection {editor.SelectionStart} to {editor.SelectionEnd}, the original's {read.Start} to {read.End}");
                    break;
                case "VK":
                    switch (Convert.ToInt32(value, 16))
                    {
                        case 0x0D:
                            Assert.Equal(shots.Length, shot);
                            return LocalSetupPolicy.NameAfterModalEntry(name, editor.Text);
                        case 0x1B:
                            Assert.Equal(shots.Length, shot);
                            return name;
                        case var key:
                            Assert.True(editor.Press(key switch
                            {
                                0x08 => Keys.Back,
                                0x23 => Keys.End,
                                0x24 => Keys.Home,
                                0x25 => Keys.Left,
                                0x27 => Keys.Right,
                                0x2E => Keys.Delete,
                                _ => throw new InvalidDataException($"{step}: no editing key for virtual key {key:X2}."),
                            }, shift), $"{step}: virtual key {key:X2} is not an editing key.");
                            break;
                    }
                    break;
                default:
                    throw new InvalidDataException($"{step}: unknown token {match.Value}.");
            }
        }
        Assert.Equal(shots.Length, shot);
        return LocalSetupPolicy.NameAfterModalEntry(name, editor.Text);
    }

    // FND-UI-022: a 12-byte name record holds the length and then the characters.
    private static string Name(JsonElement entry)
    {
        var record = entry.GetProperty("name").EnumerateArray().Select(value => value.GetInt32()).ToArray();
        return new string(record.Skip(1).Take(record[0]).Select(value => (char)value).ToArray());
    }
}

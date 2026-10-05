using System.Text.Json;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// EXP-COMLINK-001 and EXP-COMLINK-002: local games of the original in which the probe drove the
/// Comlink panels of each human player in turn, pressing their controls and typing into Send, and
/// recorded after each step what the original did and its Comlink state (FMT-STATE-005). The
/// rebuild plays the same steps in a match with the same humans, through the same functions the game
/// client calls, and has to refuse and accept the same presses and hold the same messages, read
/// flags, counts and View pages. In EXP-COMLINK-001 three humans send and read messages over three
/// turns: Send refuses a computer player's card and the sender's own and a send with no recipient,
/// drops a blank draft and stores a copy for each recipient (RULE-COMLINK-002, RULE-COMLINK-003);
/// typing overwrites the four-by-40 grid (RULE-COMLINK-006); a seventeenth message drops the oldest
/// (RULE-COMLINK-001); View opens at the oldest unread message, or where it was when every message
/// has been read, refuses an empty inbox and stops at both ends (RULE-COMLINK-004); a shown message
/// is marked read and dated from its turn (RULE-COMLINK-005); and the end of a player's planning
/// drops the read messages at the front (RULE-COMLINK-007). In EXP-COMLINK-002 the only human's
/// Send and View are both refused (RULE-COMLINK-002, RULE-COMLINK-004).
/// </summary>
public sealed class OriginalComlinkExperimentTests
{
    private const int ButtonPress = 2;
    private const int Alert = 6;

    [Theory]
    [InlineData("EXP-COMLINK-001")]
    [InlineData("EXP-COMLINK-002")]
    public void TheRebuildKeepsTheOriginalsComlinkState(string experiment)
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "spec", "experiments", $"{experiment}.json")));
        foreach (var run in fixture.RootElement.GetProperty("runs").EnumerateArray())
            new Replay(run).Play();
    }

    private sealed class Replay
    {
        private readonly JsonElement _run;
        private readonly PlayerId[] _humans;
        private readonly MatchState _match;
        private readonly MatchReplayRecorder _recorder;
        private readonly ComlinkTextEditor _editor = new();
        private readonly bool[] _selected = new bool[MatchLimits.PlayerCount];
        // The game client keeps one View page for whoever plans, as ChaosGame does.
        private int _cursor;
        private bool _sendOpen;
        private bool _viewOpen;
        private PlayerId _active;
        private string? _lastDraft;

        public Replay(JsonElement run)
        {
            _run = run;
            _humans = run.GetProperty("humans").EnumerateArray().Select(slot => new PlayerId(slot.GetInt32())).ToArray();
            // Comlink makes no draw, so the scenario and the computer players' play do not matter
            // here; only who is human and the turn count do.
            var players = Enumerable.Range(0, MatchLimits.PlayerCount)
                .Select(slot => _humans.Contains(new PlayerId(slot))
                    ? new MatchPlayerSetup(new PlayerId(slot), $"HUMAN{slot}", PlayerController.Human, (short)slot)
                    : new MatchPlayerSetup(new PlayerId(slot), $"CPU{slot}", PlayerController.Computer))
                .ToArray();
            _match = OriginalMatchFactory.Create(BundledOriginalData.Load(), new MatchSetup(
                ScenarioId.Greed, GameDuration.SixMonths, run.GetProperty("rng_state").GetInt32(), players));
            _match.FinishUpkeep();
            _recorder = new MatchReplayRecorder(_match);
        }

        public void Play()
        {
            foreach (var step in _run.GetProperty("steps").EnumerateArray())
            {
                var line = step.GetProperty("step").GetString()!;
                var space = line.IndexOf(' ');
                var verb = space < 0 ? line : line[..space];
                var argument = space < 0 ? "" : line[(space + 1)..];
                var sounds = step.GetProperty("sounds").EnumerateArray().Select(value => value.GetInt32())
                    .Where(slot => slot is not (ButtonPress or Alert)).ToArray();
                var refused = sounds.Contains(GeneralSoundSlot.RejectedInput);
                switch (verb)
                {
                    case "visit":
                        Visit(new PlayerId(int.Parse(argument, System.Globalization.CultureInfo.InvariantCulture)), step);
                        break;
                    case "view":
                        View(line, step, refused);
                        break;
                    case "next":
                    case "prev":
                        Step(line, step, refused, verb == "next" ? 1 : -1);
                        break;
                    case "dismiss":
                        _viewOpen = false;
                        break;
                    case "send":
                        OpenSend(line, step, refused);
                        break;
                    case "card":
                        Card(line, refused, int.Parse(argument, System.Globalization.CultureInfo.InvariantCulture));
                        break;
                    case "press" when argument == "send":
                        PressSend(line, refused);
                        break;
                    case "press" when argument == "cancel":
                        _sendOpen = false;
                        break;
                    case "type":
                        Type(line, refused, argument);
                        break;
                    case "done":
                        Done(line, step);
                        break;
                    case "dump" or "draft":
                        break;
                    default:
                        throw new InvalidOperationException($"No replay for the step {line}.");
                }

                Assert.True(_sendOpen == step.GetProperty("send_open").GetBoolean(), $"{line}: the original's Send panel is {(_sendOpen ? "closed" : "open")}");
                Assert.True(_viewOpen == step.GetProperty("view_open").GetBoolean(), $"{line}: the original's View panel is {(_viewOpen ? "closed" : "open")}");
                if (step.TryGetProperty("selected", out var selected))
                    Assert.True(selected.EnumerateArray().Select(value => value.GetInt32() != 0).SequenceEqual(_selected),
                        $"{line}: the original selected [{selected}]");
                if (step.TryGetProperty("draft", out var draft))
                {
                    // RULE-COMLINK-002: a refused Send returns before it prepares the draft, so
                    // the buffer keeps what the last opening left, or nothing before the first.
                    if (verb == "send" && !_sendOpen)
                        Assert.True(_lastDraft is null ? draft[0].GetInt32() == 0 : draft.GetRawText() == _lastDraft,
                            $"{line}: the refused Send changed the draft");
                    else AssertDraft(line, draft);
                    _lastDraft = draft.GetRawText();
                }
                if (step.TryGetProperty("comlink", out var comlink)) AssertComlink(line, verb, step, comlink);
            }
        }

        // RULE-SETUP-008: each human plans in slot order; the computer players and the resolution
        // run between them.
        private void Visit(PlayerId player, JsonElement step)
        {
            while (!(_match.Coordinator.Phase == TurnPhase.Command && _match.Coordinator.ActivePlayer == player))
            {
                Assert.False(_match.Coordinator.Phase == TurnPhase.Command
                    && _humans.Contains(_match.Coordinator.ActivePlayer!.Value),
                    $"visit {player.Value}: the rebuild reached human {_match.Coordinator.ActivePlayer} first");
                HeadlessMatchRunner.Advance(_recorder);
            }
            _active = player;
            Assert.Equal(step.GetProperty("active_player").GetInt32(), player.Value);
            // The original counts the turns completed, the rebuild the turn being played.
            Assert.Equal(step.GetProperty("elapsed_turns").GetInt32(), _match.Coordinator.Turn - 1);
        }

        // RULE-COMLINK-004: an empty inbox refuses View; otherwise it opens at the oldest unread
        // message, or on the page it showed last when none is unread.
        private void View(string line, JsonElement step, bool refused)
        {
            var inbox = _match.ComlinkFor(_active);
            Assert.True(step.GetProperty("entered").EnumerateArray().Any(value => value.GetString() == "view"),
                $"{line}: the original's View handler was not called");
            Assert.True(refused == (inbox.Count == 0), $"{line}: the original {(refused ? "refused" : "opened")} View with {inbox.Count} messages in the rebuild");
            if (inbox.Count == 0) return;
            _cursor = ChaosGame.InitialComlinkViewCursor(inbox, _cursor);
            Show(line, step);
            _viewOpen = true;
        }

        // FND-COMLINK-002: Previous and Next stop at the first and the last message.
        private void Step(string line, JsonElement step, bool refused, int delta)
        {
            var count = _match.ComlinkFor(_active).Count;
            var next = BoundedPageNavigation.Move(_cursor, count, delta);
            Assert.True(refused == (AudioRouting.PageNavigationSound(next != _cursor) == GeneralSoundSlot.RejectedInput),
                $"{line}: the original {(refused ? "refused" : "took")} the step from page {_cursor + 1} of {count}");
            if (next == _cursor)
            {
                Assert.False(step.TryGetProperty("shows", out _), $"{line}: the original showed a message after a refused step");
                return;
            }
            _cursor = next;
            Show(line, step);
        }

        // RULE-COMLINK-005: the message shown is marked read, with its page, the count and the
        // year and week of its turn drawn.
        private void Show(string line, JsonElement step)
        {
            var inbox = _match.ComlinkFor(_active);
            var shows = step.GetProperty("shows").EnumerateArray().ToArray();
            Assert.Single(shows);
            var show = shows[0];
            Assert.Equal(_active.Value, show.GetProperty("player").GetInt32());
            Assert.Equal(inbox.Count, show.GetProperty("count").GetInt32());
            Assert.True(show.GetProperty("cursor").GetInt32() == _cursor,
                $"{line}: the original showed page {show.GetProperty("cursor").GetInt32() + 1}, the rebuild page {_cursor + 1}");
            var message = inbox.Messages[_cursor];
            var (year, week) = MatchCalendar.Of(message.Turn - 1);
            Assert.Equal([_cursor + 1, inbox.Count, year, week],
                show.GetProperty("numbers").EnumerateArray().Select(value => value.GetInt32()).ToArray());
            if (!inbox.IsRead(message.Sequence)) Assert.True(_recorder.MarkComlinkRead(_active, message.Sequence));
        }

        // RULE-COMLINK-002: Send opens with no recipient selected and a blank draft, or is refused
        // when no other human can take a message.
        private void OpenSend(string line, JsonElement step, bool refused)
        {
            Assert.True(step.GetProperty("entered").EnumerateArray().Any(value => value.GetString() == "send"),
                $"{line}: the original's Send handler was not called");
            var opens = _match.HasComlinkRecipient(_active);
            Assert.True(refused != opens, $"{line}: the original {(refused ? "refused" : "opened")} Send");
            if (!opens) return;
            Array.Fill(_selected, false);
            _editor.Clear();
            _sendOpen = true;
        }

        // FND-COMLINK-003: a card press flips the selection of a player who can take a message and
        // is refused for any other.
        private void Card(string line, bool refused, int slot)
        {
            var eligible = _match.IsComlinkRecipient(_active, new PlayerId(slot));
            Assert.True(refused != eligible, $"{line}: the original {(refused ? "refused" : "took")} the card");
            if (eligible) _selected[slot] = !_selected[slot];
        }

        // RULE-COMLINK-003: Send with no recipient is refused and the panel stays open; otherwise
        // the panel closes and each recipient gets a copy, unless the draft is blank.
        private void PressSend(string line, bool refused)
        {
            var recipients = Enumerable.Range(0, MatchLimits.PlayerCount).Where(slot => _selected[slot])
                .Select(slot => new PlayerId(slot)).ToArray();
            var result = _recorder.SendComlinkMessage(_active, recipients, _editor.Text);
            Assert.True(refused != result.Accepted, $"{line}: the original {(refused ? "refused" : "took")} the send: {result.Message}");
            if (result.Accepted) _sendOpen = false;
        }

        // RULE-COMLINK-006: each key as the original's window procedure delivers it, a virtual key
        // and the unshifted character; the rebuild takes the character through the key map the
        // game client uses.
        private void Type(string line, bool refused, string text)
        {
            Assert.False(refused, $"{line}: the original refused a key");
            for (var index = 0; index < text.Length; index++)
            {
                if (text[index] == '{')
                {
                    var end = text.IndexOf('}', index);
                    var name = text[(index + 1)..end];
                    index = end;
                    switch (name)
                    {
                        case "BACK": _editor.Backspace(); break;
                        case "ENTER": _editor.MoveNextRow(); break;
                        case "LEFT": _editor.MoveLeft(); break;
                        case "UP": _editor.MoveUp(); break;
                        case "RIGHT": _editor.MoveRight(); break;
                        case "DOWN": _editor.MoveDown(); break;
                        // FND-COMLINK-003: Execute sends as the Send control does.
                        case "EXEC": PressSend(line, refused: false); break;
                        default: throw new InvalidOperationException($"No key {name}.");
                    }
                    // An accepted send closes the panel, and the original hands any later key to
                    // the city screen, which this replay does not model.
                    if (!_sendOpen && index + 1 < text.Length)
                        throw new InvalidOperationException($"{line}: keys follow a send that closed the panel.");
                    continue;
                }
                if (OriginalTextInput.TryCharacter(Key(text[index]), shift: false, out var character))
                    _editor.TryAppend(character);
            }
        }

        private static Keys Key(char character) => character switch
        {
            >= 'A' and <= 'Z' => Keys.A + (character - 'A'),
            >= 'a' and <= 'z' => Keys.A + (character - 'a'),
            >= '0' and <= '9' => Keys.D0 + (character - '0'),
            ' ' => Keys.Space,
            ';' => Keys.OemSemicolon,
            '=' => Keys.OemPlus,
            ',' => Keys.OemComma,
            '-' => Keys.OemMinus,
            '.' => Keys.OemPeriod,
            '/' => Keys.OemQuestion,
            '`' => Keys.OemTilde,
            '[' => Keys.OemOpenBrackets,
            '\\' => Keys.OemPipe,
            ']' => Keys.OemCloseBrackets,
            '\'' => Keys.OemQuotes,
            _ => throw new InvalidOperationException($"No key types {character}."),
        };

        // RULE-COMLINK-007: Done ends the player's planning and drops the read messages at the front.
        private void Done(string line, JsonElement step)
        {
            var before = _match.ComlinkFor(_active).Count;
            _recorder.FinishCommand(_active);
            var after = _match.ComlinkFor(_active).Count;
            var drops = step.GetProperty("drops").EnumerateArray().ToArray();
            Assert.Single(drops);
            Assert.Equal([_active.Value, before, after], drops[0].EnumerateArray().Select(value => value.GetInt32()).ToArray());
        }

        // FMT-STATE-005: the draft is a record of the active player's, stamped with the turn, with
        // the 160 cells of the grid.
        private void AssertDraft(string line, JsonElement draft)
        {
            var record = draft.EnumerateArray().ToArray();
            Assert.Equal(1, record[0].GetInt32());
            Assert.Equal(0, record[1].GetInt32());
            Assert.Equal(_match.Coordinator.Turn - 1, record[2].GetInt32());
            Assert.Equal(_active.Value, record[3].GetInt32());
            Assert.Equal(0, record[4].GetInt32());
            var cells = string.Concat(_editor.DisplayLines());
            Assert.True(record[5].GetString() == cells.TrimEnd(' '),
                $"{line}: the original's draft is [{record[5].GetString()}], the rebuild's [{cells.TrimEnd(' ')}]");
        }

        // FMT-STATE-005, RULE-COMLINK-001: each human's count and records, the messages first and
        // every record after them empty with read set; comlink_pending; and the View page while
        // the planning player's View is in use.
        private void AssertComlink(string line, string verb, JsonElement step, JsonElement comlink)
        {
            var counts = comlink.GetProperty("counts").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var cursors = comlink.GetProperty("cursors").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            foreach (var human in _humans)
            {
                var inbox = _match.ComlinkFor(human);
                Assert.True(counts[human.Value] == inbox.Count,
                    $"{line}: player {human.Value} holds {counts[human.Value]} messages in the original, {inbox.Count} in the rebuild");
                var records = comlink.GetProperty("records").GetProperty(human.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .EnumerateArray().Select(record => record.EnumerateArray().ToArray()).ToArray();
                Assert.Equal(MatchLimits.ComlinkMessagesPerPlayer, records.Length);
                for (var index = 0; index < records.Length; index++)
                {
                    var record = records[index];
                    if (index >= inbox.Count)
                    {
                        Assert.True(record[0].GetInt32() == 0 && record[1].GetInt32() == 1,
                            $"{line}: player {human.Value} record {index} is not empty and read");
                        continue;
                    }
                    var message = inbox.Messages[index];
                    var expected = $"[1, {(inbox.IsRead(message.Sequence) ? 1 : 0)}, {message.Turn - 1}, {message.Sender.Value}, 0, {message.Text}]";
                    var actual = $"[{record[0].GetInt32()}, {record[1].GetInt32()}, {record[2].GetInt32()}, {record[3].GetInt32()}, {record[4].GetInt32()}, {record[5].GetString()}]";
                    Assert.True(expected == actual, $"{line}: player {human.Value} record {index} is {actual} in the original, {expected} in the rebuild");
                }
            }

            // comlink_pending follows the planning player's unread messages from its planning entry.
            if (verb != "done" && _humans.Contains(new PlayerId(step.GetProperty("active_player").GetInt32())))
                Assert.True((comlink.GetProperty("pending").GetInt32() != 0) == _match.ComlinkFor(_active).HasUnread,
                    $"{line}: comlink_pending is {comlink.GetProperty("pending").GetInt32()}");
            if (verb is "view" or "dismiss" && _match.ComlinkFor(_active).Count > 0)
                Assert.True(cursors[_active.Value] == _cursor,
                    $"{line}: the original's View page is {cursors[_active.Value] + 1}, the rebuild's {_cursor + 1}");
        }
    }
}

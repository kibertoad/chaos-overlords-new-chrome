using System.Runtime.InteropServices;

namespace Rechaos.OriginalProbe;

/// <summary>
/// A key event the window procedure built from a posted <c>WM_KEYDOWN</c> (FND-UI-020): the virtual
/// key posted, whether the probe made the Shift test report the key held, and the event's type,
/// character and key as the procedure stored them.
/// </summary>
internal sealed record KeyEventRecord(int VirtualKey, bool Shift, int Type, int Character, int Key);

/// <summary>
/// A name typed into the setup name editor, dialog 139 (FND-UI-022): the keys as the step gave
/// them and the 12-byte name record of the slot after OK, its length byte first.
/// </summary>
internal sealed record NameEntryRecord(string Keys, int Slot, List<int> Name);

internal sealed partial class NewGameSession
{
    private readonly List<KeyEventRecord> _keyEvents = [];
    private readonly List<NameEntryRecord> _nameEntries = [];
    private bool? _forcedShift;
    private int _postedKey = -1;
    private bool _keyEventsArmed;

    // keys:TOKENS and name:TOKENS take {VKhh} for a press of virtual key hh, {CHARhh} for the
    // character hh posted as WM_CHAR, {SHIFT} and {PLAIN} for Shift held and released.
    internal static IEnumerable<(string Kind, int Value)> KeyTokens(string text)
    {
        for (var index = 0; index < text.Length;)
        {
            var end = text.IndexOf('}', index);
            if (text[index] != '{' || end < 0) throw new FormatException($"Key tokens are {{VKhh}}, {{CHARhh}}, {{SHIFT}} or {{PLAIN}}: {text}");
            var token = text[(index + 1)..end];
            index = end + 1;
            yield return token switch
            {
                "SHIFT" => ("shift", 1),
                "PLAIN" => ("shift", 0),
                _ when token.StartsWith("VK", StringComparison.Ordinal) =>
                    ("vk", Convert.ToInt32(token[2..], 16)),
                _ when token.StartsWith("CHAR", StringComparison.Ordinal) =>
                    ("char", Convert.ToInt32(token[4..], 16)),
                _ => throw new FormatException($"Unknown key token {token}."),
            };
        }
    }

    // FND-UI-064: the window procedure tests Shift with GetAsyncKeyState, which a posted message
    // cannot set, so the probe gives the call's result itself, and records the event the
    // procedure stores once its switch has run.
    private void ArmKeyEvents()
    {
        if (_keyEventsArmed) return;
        _keyEventsArmed = true;
        _process.SetBreakpoint(OriginalAddresses.ShiftTested, context =>
        {
            if (_forcedShift is { } held) context.Eax = held ? 0xFFFF8000u : 0u;
        }, quiet: true);
        _process.SetBreakpoint(OriginalAddresses.KeyEventStored, _ => _keyEvents.Add(new KeyEventRecord(
            _postedKey, _forcedShift == true,
            _process.ReadInt32(OriginalAddresses.InputEvent),
            _process.ReadInt32(OriginalAddresses.InputEvent + 4),
            _process.ReadInt32(OriginalAddresses.InputEvent + 8))), quiet: true);
    }

    // --order-steps keys:TOKENS: a key press of the game window for each virtual key, with the
    // Shift test's result the tokens give.
    private void PressKeys(IntPtr window, string tokens)
    {
        ArmKeyEvents();
        foreach (var (kind, value) in KeyTokens(tokens))
        {
            if (kind == "shift")
            {
                _forcedShift = value != 0;
                continue;
            }
            if (kind != "vk") throw new FormatException("keys: takes {VKhh}, {SHIFT} and {PLAIN} only.");
            _postedKey = value;
            Native.PostMessageW(window, Native.WmKeyDown, value, KeyParameter(value, up: false));
            _process.Pump(TimeSpan.FromSeconds(0.12));
            Native.PostMessageW(window, Native.WmKeyUp, value, KeyParameter(value, up: true));
            _process.Pump(TimeSpan.FromSeconds(0.05));
        }
        _forcedShift = null;
        _postedKey = -1;
        _process.Pump(TimeSpan.FromSeconds(0.3));
    }

    // The lParam of a key message: repeat count 1 and the key's scan code, with the previous-state
    // and transition bits of a release.
    private static IntPtr KeyParameter(int virtualKey, bool up) =>
        unchecked((IntPtr)(int)(1u | ((Native.MapVirtualKeyW((uint)virtualKey, 0) & 0xFF) << 16) | (up ? 0xC0000000u : 0u)));

    // --setup-steps name:TOKENS: a press on the name band of card 0, which is selected, opens the
    // name editor, dialog 139 (FND-SETUP-005, FND-UI-022). Its edit control takes the keys as the
    // dialog's own message loop translates them, so Shift is set in the keyboard state the probe
    // shares with the game's thread for the time of the keys. OK closes the dialog, and the name
    // record of slot 0 is read.
    private void EnterName(IntPtr window, string tokens)
    {
        _process.Pump(TimeSpan.FromSeconds(1));
        Click(window, OriginalAddresses.NameBandX, OriginalAddresses.NameBandY);
        var dialog = IntPtr.Zero;
        if (!_process.RunUntil(() => (dialog = FindDialog()) != IntPtr.Zero, TimeSpan.FromSeconds(5)))
        {
            _notes.Add($"The name editor never opened for name:{tokens}.");
            return;
        }
        _process.Pump(TimeSpan.FromSeconds(0.5));
        var edit = Native.GetDlgItem(dialog, OriginalAddresses.NameEditControl);
        var thread = Native.GetWindowThreadProcessId(dialog, out _);
        var attached = false;
        try
        {
            foreach (var (kind, value) in KeyTokens(tokens))
            {
                switch (kind)
                {
                    case "shift":
                        if (!attached)
                        {
                            // AttachThreadInput needs a message queue on this thread too.
                            Native.PeekMessageW(out _, IntPtr.Zero, 0, 0, 0);
                            attached = Native.AttachThreadInput(Native.GetCurrentThreadId(), thread, true);
                            if (!attached) _notes.Add($"AttachThreadInput failed with {Marshal.GetLastWin32Error()}.");
                        }
                        SetSharedShift(value != 0);
                        break;
                    case "vk":
                        Native.PostMessageW(edit, Native.WmKeyDown, value, KeyParameter(value, up: false));
                        _process.Pump(TimeSpan.FromSeconds(0.12));
                        Native.PostMessageW(edit, Native.WmKeyUp, value, KeyParameter(value, up: true));
                        _process.Pump(TimeSpan.FromSeconds(0.05));
                        break;
                    case "char":
                        Native.PostMessageW(edit, Native.WmChar, value, 1);
                        _process.Pump(TimeSpan.FromSeconds(0.12));
                        break;
                }
            }
            _process.Pump(TimeSpan.FromSeconds(0.3));
        }
        finally
        {
            if (attached)
            {
                SetSharedShift(false);
                Native.AttachThreadInput(Native.GetCurrentThreadId(), thread, false);
            }
        }
        Native.PostMessageW(dialog, Native.WmCommand, 1, Native.GetDlgItem(dialog, 1));
        if (!_process.RunUntil(() => !Native.IsWindow(dialog), TimeSpan.FromSeconds(5)))
            _notes.Add($"The name editor did not close after OK for name:{tokens}.");
        _process.Pump(TimeSpan.FromSeconds(0.5));
        _nameEntries.Add(new NameEntryRecord(tokens, 0, _process.Read(OriginalAddresses.RosterNames, OriginalAddresses.RosterNameLength)
            .Select(value => (int)value).ToList()));
    }

    private static void SetSharedShift(bool held)
    {
        var state = new byte[256];
        Native.GetKeyboardState(state);
        foreach (var key in new[] { 0x10, 0xA0 }) state[key] = held ? (byte)0x80 : (byte)0;
        Native.SetKeyboardState(state);
    }

    // The visible dialog of the original's process.
    private IntPtr FindDialog()
    {
        var found = IntPtr.Zero;
        var name = new char[16];
        Native.EnumWindows((candidate, _) =>
        {
            Native.GetWindowThreadProcessId(candidate, out var owner);
            if (owner != _process.ProcessId || !Native.IsWindowVisible(candidate)) return true;
            var length = Native.GetClassNameW(candidate, name, name.Length);
            if (new string(name, 0, length) != "#32770") return true;
            found = candidate;
            return false;
        }, IntPtr.Zero);
        return found;
    }
}

namespace Rechaos.OriginalProbe;

// The screens new-game copies before the match: the title, the credits and the setup screen
// (FND-UI-055).
internal sealed partial class NewGameSession
{
    // --credits-capture: Help, About from the title (FND-UI-007). The breakpoint after the load of
    // the credits art says they are being shown; a key press closes them.
    private void CaptureCredits(IntPtr window)
    {
        var shown = false;
        _process.SetBreakpoint(OriginalAddresses.CreditsArtLoaded, _ => shown = true, oneShot: true);
        Native.PostMessageW(window, Native.WmCommand, OriginalAddresses.AboutCommand, IntPtr.Zero);
        if (!_process.RunUntil(() => shown, TimeSpan.FromSeconds(10)))
        {
            _notes.Add("The credits never opened.");
            return;
        }
        _process.Pump(TimeSpan.FromSeconds(2));
        CaptureBeforeMatch(window, "credits");
        Native.PostMessageW(window, Native.WmKeyDown, 0x20, IntPtr.Zero);
        Native.PostMessageW(window, Native.WmKeyUp, 0x20, IntPtr.Zero);
        _process.Pump(TimeSpan.FromSeconds(2));
    }

    // --title-capture, --credits-capture, --setup-capture: copies the screen shown before the match
    // as <name>-capture.bmp and notes it for extract when two agreeing copies were taken.
    private void CaptureBeforeMatch(IntPtr window, string name)
    {
        if (ClientAreaHoldsDrawingArea(window)
            && CaptureDrawingArea(window, $"{name}-capture", CaptureFixture.Width, CaptureFixture.Height))
            _notes.Add(CaptureFixture.BeforeMatchNote(name));
        else
            _notes.Add($"The {name} screen was not captured.");
    }

    // A smaller client area leaves part of the copy outside the window, and that part is not the
    // original's drawing.
    private bool ClientAreaHoldsDrawingArea(IntPtr window)
    {
        const int width = CaptureFixture.Width, height = CaptureFixture.Height;
        if (!Native.GetClientRect(window, out var client)
            || client.Right - client.Left < width || client.Bottom - client.Top < height)
        {
            _notes.Add($"Capture rejected: the client area is {client.Right - client.Left} by "
                + $"{client.Bottom - client.Top}, smaller than the {width}-by-{height} drawing area.");
            return false;
        }
        _notes.Add($"Client area {client.Right - client.Left} by {client.Bottom - client.Top}.");
        return true;
    }
}

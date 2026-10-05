namespace Rechaos.OriginalProbe;

// The screens new-game copies before the match: the credits and the setup steps. The title and
// the setup screen as it opens are copied in the session's run itself.
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
        if (CaptureDrawingArea(window, "credits-capture", CaptureFixture.Width, CaptureFixture.Height))
            _notes.Add(CaptureFixture.BeforeMatchNote("credits"));
        else
            _notes.Add("The credits were not captured.");
        Native.PostMessageW(window, Native.WmKeyDown, 0x20, IntPtr.Zero);
        Native.PostMessageW(window, Native.WmKeyUp, 0x20, IntPtr.Zero);
        _process.Pump(TimeSpan.FromSeconds(2));
    }

    // A press at (x, y), moves with the button down in eight steps to (toX, toY), and the release
    // there, each message given time to be read. A drag is told from a press only while the button
    // is down (FND-SETUP-005), so the game is first left to finish what the step before started,
    // such as a refusal sound, or it reads the press after the release and takes it for a click.
    private void Drag(IntPtr window, int x, int y, int toX, int toY)
    {
        _process.Pump(TimeSpan.FromSeconds(1.5));
        Native.PostMessageW(window, Native.WmMouseMove, IntPtr.Zero, PointParameter(x, y));
        Native.PostMessageW(window, Native.WmLButtonDown, 1, PointParameter(x, y));
        _process.Pump(TimeSpan.FromSeconds(0.6));
        for (var step = 1; step <= 8; step++)
        {
            Native.PostMessageW(window, Native.WmMouseMove, 1,
                PointParameter(x + (toX - x) * step / 8, y + (toY - y) * step / 8));
            _process.Pump(TimeSpan.FromSeconds(0.1));
        }
        Native.PostMessageW(window, Native.WmLButtonUp, IntPtr.Zero, PointParameter(toX, toY));
        _process.Pump(TimeSpan.FromSeconds(0.8));
    }

    // --setup-steps: presses on the setup screen, and copies of it after them, before the run
    // writes its own settings, which replace whatever the presses chose.
    private void RecordSetupSteps(IntPtr window, IReadOnlyList<ProbeOrderStep>? steps)
    {
        for (var index = 0; index < (steps?.Count ?? 0); index++)
        {
            var step = steps![index];
            if (step.Kind == "strip")
            {
                Click(window, step.X, step.Y);
                _process.Pump(TimeSpan.FromSeconds(0.8));
            }
            else if (step.Kind == "drag")
                Drag(window, step.X, step.Y, step.Target, step.Choice);
            else if (CaptureDrawingArea(window, $"setup-step-{index}", CaptureFixture.Width, CaptureFixture.Height))
                _notes.Add(CaptureFixture.SetupStepNote(index));
            else
                _notes.Add($"Setup step {index} was not captured.");
        }
    }
}

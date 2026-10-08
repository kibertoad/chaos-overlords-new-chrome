namespace Rechaos.OriginalProbe;

// The screens new-game copies before the match: the title, the credits and the setup screen
// (FND-UI-055), and the setup screen after presses on it.
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

    // A press at (x, y), the pointer moved with the button down in eight steps to (toX, toY), and
    // the release there, each given time to be read. A drag is told from a press only while the
    // button is down (FND-SETUP-005), so the game is first left to finish what the step before
    // started, such as a refusal sound, or it reads the press after the release and takes it for a
    // click. The game follows the two pointer points the window procedure keeps from WM_MOUSEMOVE,
    // one of them from GetCursorPos (FND-UI-020), so the probe writes both itself, as the hire
    // steps do, instead of posting moves that would put the desktop's cursor in one of them.
    private void Drag(IntPtr window, int x, int y, int toX, int toY)
    {
        _process.Pump(TimeSpan.FromSeconds(1.5));
        PointerAt(x, y);
        Post(window, Native.WmLButtonDown, 1, x, y);
        _process.Pump(TimeSpan.FromSeconds(0.6));
        for (var step = 1; step <= 8; step++)
        {
            PointerAt(x + (toX - x) * step / 8, y + (toY - y) * step / 8);
            _process.Pump(TimeSpan.FromSeconds(0.1));
        }
        // A WM_MOUSEMOVE the system sends meanwhile puts the desktop cursor in both points, so they
        // are written again just before the release is read.
        PointerAt(toX, toY);
        Post(window, Native.WmLButtonUp, 0, toX, toY);
        _process.Pump(TimeSpan.FromSeconds(0.8));
    }

    // --setup-steps: presses on the setup screen, and copies of it after them, before the run
    // writes its own settings. ApplySettings writes only the settings the run was given, so any
    // other choice stays as the presses left it, and the trace notes which (SetupStepsNote).
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
            else if (step.Kind == "name")
                EnterName(window, step.Text!);
            else if (ClientAreaHoldsDrawingArea(window)
                     && CaptureDrawingArea(window, $"setup-step-{index}", CaptureFixture.Width, CaptureFixture.Height))
                _notes.Add(CaptureFixture.SetupStepNote(index));
            else
                _notes.Add($"Setup step {index} was not captured.");
        }
    }

    // The setup choices the run's own settings leave as the screen holds them (ApplySettings), read
    // before and after the setup steps.
    private List<(string Name, byte[] Value)> SetupChoicesLeftToPresses()
    {
        var choices = new List<(string Name, byte[] Value)>();
        if (settings.Scenario is null)
        {
            choices.Add(("scenario", _process.Read(OriginalAddresses.Scenario, 4)));
            choices.Add(("preferred scenario", _process.Read(OriginalAddresses.PreferredScenario, 1)));
        }
        if (settings.TurnLimit is null) choices.Add(("length", _process.Read(OriginalAddresses.TurnLimit, 4)));
        if (settings.Mentality is null) choices.Add(("Mentality", _process.Read(OriginalAddresses.Mentality, 1)));
        if (settings.TimeLimit is null)
            choices.Add(("turn time", _process.Read(OriginalAddresses.PlanningLimitChoice, 1)));
        if (settings.Humans is null)
        {
            choices.Add(("player types", _process.Read(OriginalAddresses.RosterTypes, 24)));
            choices.Add(("portraits", _process.Read(OriginalAddresses.RosterPortraits, 6)));
        }
        return choices;
    }

    // The choices the run's settings do not write that the setup steps left changed: the match
    // starts with what the presses chose there, which a fixture's settings alone do not say.
    // Null when every such choice is as the screen opened with it.
    private string? SetupStepsNote(List<(string Name, byte[] Value)> before)
    {
        var changed = SetupChoicesLeftToPresses()
            .Where((choice, index) => !choice.Value.AsSpan().SequenceEqual(before[index].Value))
            .Select(choice => choice.Name)
            .ToArray();
        return changed.Length == 0
            ? null
            : $"The setup steps left the {string.Join(", ", changed)} changed, which the run's settings do not set.";
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

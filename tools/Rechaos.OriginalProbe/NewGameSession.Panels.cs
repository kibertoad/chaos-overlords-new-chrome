namespace Rechaos.OriginalProbe;

internal sealed partial class NewGameSession
{
    // Kept in the order of the calls, as not shown until the handler reaches its call of the
    // panel-open helper (FND-UI-061), whatever closes the panel afterwards.
    private void OpenPanel(BreakContext context, string panel)
    {
        _notes.Add($"{panel} opened after roll {_rolls.Count}");
        var index = _panels.Count;
        _panels.Add(new PanelRecord(panel, _rolls.Count, false));
        _openPanelCalls.Add(index);
        _process.SetBreakpoint(context.ReturnAddress, _ => _openPanelCalls.Remove(index), oneShot: true);
    }

    // FND-UI-061: the innermost open call of the panel has reached its slide-in.
    private void PanelShown(string panel)
    {
        for (var at = _openPanelCalls.Count - 1; at >= 0; at--)
        {
            var index = _openPanelCalls[at];
            if (_panels[index].Panel != panel) continue;
            _panels[index] = _panels[index] with { Shown = true };
            return;
        }
        _notes.Add($"{panel} slid in after roll {_rolls.Count} with no call of its handler open");
    }

    // Presses Exit until every panel handler that opened has returned. FND-UI-061: Last Turn Events
    // is a call of its own that the planning entry makes after Combat Results has returned, so once
    // a press has closed a panel the probe waits for the next one to open or for the planning loop
    // to run before it counts the panels closed.
    private bool ClosePanels(IntPtr window)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (PanelsOpen == 0)
            {
                if (attempt == 0) return true;
                var loopReached = false;
                Action<BreakContext> onLoop = _ => loopReached = true;
                _process.SetBreakpoint(OriginalAddresses.PlanningTimeCheck, onLoop, oneShot: true);
                _process.RunUntil(() => loopReached || PanelsOpen > 0, TimeSpan.FromSeconds(3));
                // When a panel opened or the 3 seconds ran out first, the handler is still on the
                // time check and would fire on a later pass.
                _process.RemoveBreakpoint(OriginalAddresses.PlanningTimeCheck, onLoop);
                if (PanelsOpen == 0) return true;
                // The handler has only been entered. Its slide-in blocks input (FND-UI-011), so the
                // press waits until the panel is quiet, as for a panel at a planning entry.
                _process.RunUntil(
                    () => PanelsOpen == 0 || DateTime.UtcNow - _process.LastBreakpointUtc > TimeSpan.FromSeconds(0.5),
                    TimeSpan.FromSeconds(3));
                if (PanelsOpen == 0) continue;
            }
            Click(window, OriginalAddresses.PanelExitX, OriginalAddresses.PanelExitY);
            _process.RunUntil(() => PanelsOpen == 0, TimeSpan.FromSeconds(3));
        }

        return PanelsOpen == 0;
    }

    // An Exit press of a step after the dump. With no panel open the Exit point lies on the city
    // map, where a press would select a sector and a second one open the sector view, so the
    // press is skipped.
    private void PressExitAfterDump(IntPtr window)
    {
        if (PanelsOpen == 0)
        {
            _notes.Add("exit after the dump skipped: no panel was open");
            return;
        }
        Click(window, OriginalAddresses.PanelExitX, OriginalAddresses.PanelExitY);
    }
}

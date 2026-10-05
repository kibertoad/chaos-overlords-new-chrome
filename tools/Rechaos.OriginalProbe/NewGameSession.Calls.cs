namespace Rechaos.OriginalProbe;

/// <summary>A call of one of the original's cdecl functions that the probe makes itself.</summary>
internal sealed record InjectedCall(uint Function, int[] Arguments, Action Returned);

internal sealed partial class NewGameSession
{
    // Once the dump is taken, the probe can call functions of the original itself, as the
    // original's own code calls them. It starts from the next message pump call (FND-UI-020), so a
    // result panel still open at the endpoint does not stop it, saves every register there, and
    // runs the calls one after another below the saved stack pointer. Each call returns to
    // InjectedCallReturn, where a one-shot breakpoint reads what the call left and starts the
    // next one; after the last, every register is put back. The Equip list builder and the Attack
    // picker's roster builder read active_player rather than a player they are passed
    // (FND-EQUIP-008, FND-ATTACK-006), and their panels open only for the active player's gangs,
    // so active_player holds the given player during the calls and is put back after the last.
    private bool RunInjectedCalls(Queue<InjectedCall> calls, int player)
    {
        if (calls.Count == 0) return true;
        byte[]? saved = null;
        uint stack = 0;
        var active = 0;
        var finished = false;
        InjectedCall? current = null;

        void Call(BreakContext context)
        {
            current = calls.Dequeue();
            var esp = stack - 0x40;
            _process.Write(esp, [
                .. BitConverter.GetBytes(OriginalAddresses.InjectedCallReturn),
                .. current.Arguments.SelectMany(BitConverter.GetBytes)]);
            context.Esp = esp;
            context.Eip = current.Function;
            _process.SetBreakpoint(OriginalAddresses.InjectedCallReturn, Returned, oneShot: true, quiet: true);
        }

        void Returned(BreakContext context)
        {
            current!.Returned();
            if (calls.Count > 0)
            {
                Call(context);
                return;
            }
            _process.Write(OriginalAddresses.ActivePlayer, BitConverter.GetBytes(active));
            context.Restore(saved!);
            finished = true;
        }

        foreach (var peek in OriginalAddresses.PumpPeeks)
            _process.SetBreakpoint(peek, context =>
            {
                if (saved is not null) return;
                saved = context.Save();
                stack = context.Esp;
                active = _process.ReadInt32(OriginalAddresses.ActivePlayer);
                _process.Write(OriginalAddresses.ActivePlayer, BitConverter.GetBytes(player));
                Call(context);
            }, oneShot: true, quiet: true);
        return _process.RunUntil(() => finished, TimeSpan.FromSeconds(30));
    }

    // The first human's living gangs, as roster slot, definition and sector (FMT-STATE-001:
    // definition at 0x01, sector at 0x02, 100 for a slot with no living gang).
    private IEnumerable<(int Slot, int Definition, int Sector)> HumanGangs(int human)
    {
        for (var slot = 0; slot < OriginalAddresses.PlayerGangStride / OriginalAddresses.GangRecordSize; slot++)
        {
            var record = _process.Read(OriginalAddresses.GangRecords
                + (uint)(human * OriginalAddresses.PlayerGangStride + slot * OriginalAddresses.GangRecordSize), 3);
            if (record[2] != 100) yield return (slot, record[1], record[2]);
        }
    }
}

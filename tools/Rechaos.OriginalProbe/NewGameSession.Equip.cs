namespace Rechaos.OriginalProbe;

/// <summary>
/// The list the Equip panel offers one of the first human's gangs for one category, as the list
/// builder of FND-EQUIP-008 fills it: the item record numbers of its non-empty entries, in order.
/// </summary>
internal sealed record EquipListRecord(int Slot, int Category, int TechLevel, List<int> Items);

internal sealed partial class NewGameSession
{
    private readonly List<EquipListRecord> _equipLists = [];

    // RULE-EQUIP-004: once the dump is taken, the probe calls the original's list builder itself
    // for every category of every living gang of the first human, as the Equip panel calls it, and
    // reads the entries it fills. It starts from the next message pump call, so a result panel
    // still open at the endpoint does not stop it, and puts every register and active_player back
    // afterwards. The calls overwrite the list entries and text rows the dump holds, which is why
    // they come after it.
    private bool RecordEquipLists()
    {
        var human = FirstHuman;
        var calls = new Queue<(int Slot, int Category, int TechLevel)>();
        for (var slot = 0; slot < OriginalAddresses.PlayerGangStride / OriginalAddresses.GangRecordSize; slot++)
        {
            var record = _process.Read(OriginalAddresses.GangRecords
                + (uint)(human * OriginalAddresses.PlayerGangStride + slot * OriginalAddresses.GangRecordSize), 3);
            // FMT-STATE-001: definition at 0x01, sector at 0x02, 100 for a slot with no living gang.
            if (record[2] == 100) continue;
            var tech = BitConverter.ToInt16(_process.Read(OriginalAddresses.GangDefinitionTechLevel
                + (uint)(record[1] * OriginalAddresses.GangDefinitionSize), 2));
            for (var category = 0; category < 4; category++) calls.Enqueue((slot, category, tech));
        }
        if (calls.Count == 0)
        {
            _notes.Add($"Equip lists: player {human} has no living gang, so no list was built.");
            return true;
        }

        byte[]? saved = null;
        uint stack = 0;
        var active = 0;
        var finished = false;
        (int Slot, int Category, int TechLevel) current = default;

        void Call(BreakContext context)
        {
            current = calls.Dequeue();
            var esp = stack - 0x40;
            _process.Write(esp, [
                .. BitConverter.GetBytes(OriginalAddresses.EquipListReturn),
                .. BitConverter.GetBytes(current.Category),
                .. BitConverter.GetBytes(current.TechLevel),
                .. BitConverter.GetBytes(human),
                .. BitConverter.GetBytes(current.Slot)]);
            context.Esp = esp;
            context.Eip = OriginalAddresses.EquipListBuilder;
            _process.SetBreakpoint(OriginalAddresses.EquipListReturn, Returned, oneShot: true, quiet: true);
        }

        void Returned(BreakContext context)
        {
            var entries = _process.Read(OriginalAddresses.EquipListEntries, 4 * OriginalAddresses.EquipListLength);
            var items = Enumerable.Range(0, OriginalAddresses.EquipListLength)
                .Select(index => BitConverter.ToInt32(entries, 4 * index)).Where(item => item != -1).ToList();
            // FND-EQUIP-008: the builder's count has no upper bound, so a full list may have run past
            // the sixteen entries read here.
            if (items.Count == OriginalAddresses.EquipListLength)
                _notes.Add($"Equip list of slot {current.Slot} category {current.Category} fills all {items.Count} entries and may be longer.");
            _equipLists.Add(new EquipListRecord(current.Slot, current.Category, current.TechLevel, items));
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
                // FND-EQUIP-008: the research test reads active_player, not the player argument, and
                // the panel opens only for the active player's gangs.
                active = _process.ReadInt32(OriginalAddresses.ActivePlayer);
                _process.Write(OriginalAddresses.ActivePlayer, BitConverter.GetBytes(human));
                Call(context);
            }, oneShot: true, quiet: true);
        return _process.RunUntil(() => finished, TimeSpan.FromSeconds(30));
    }
}

namespace Rechaos.OriginalProbe;

/// <summary>
/// The list the Equip panel offers one of the first human's gangs for one category, as the list
/// builder of FND-EQUIP-008 fills it: the item record numbers of its non-empty entries, in order.
/// </summary>
internal sealed record EquipListRecord(int Slot, int Category, int TechLevel, List<int> Items);

internal sealed partial class NewGameSession
{
    private readonly List<EquipListRecord> _equipLists = [];

    // RULE-EQUIP-004: the probe calls the original's list builder for every category of every
    // living gang of the first human, as the Equip panel calls it, and reads the entries it fills.
    // The calls draw the list's rows and change nothing the fixture holds, which is why they come
    // after the dump.
    private bool RecordEquipLists()
    {
        var human = FirstHuman;
        var calls = new Queue<InjectedCall>();
        foreach (var (slot, definition, _) in HumanGangs(human).ToList())
        {
            var tech = BitConverter.ToInt16(_process.Read(OriginalAddresses.GangDefinitionTechLevel
                + (uint)(definition * OriginalAddresses.GangDefinitionSize), 2));
            for (var category = 0; category < 4; category++)
            {
                var record = new EquipListRecord(slot, category, tech, []);
                calls.Enqueue(new InjectedCall(OriginalAddresses.EquipListBuilder, [category, tech, human, slot], () =>
                {
                    var entries = _process.Read(OriginalAddresses.EquipListEntries, 4 * OriginalAddresses.EquipListLength);
                    record.Items.AddRange(Enumerable.Range(0, OriginalAddresses.EquipListLength)
                        .Select(index => BitConverter.ToInt32(entries, 4 * index)).Where(item => item != -1));
                    _equipLists.Add(record);
                }));
            }
        }
        return RunInjectedCalls(calls, human);
    }
}

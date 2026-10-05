namespace Rechaos.OriginalProbe;

/// <summary>
/// The targets the Attack picker offers one of the first human's gangs among one opponent's
/// gangs, as the roster builder of FND-ATTACK-006 fills them: the opponent's roster slots in its
/// non-empty entries, in order.
/// </summary>
internal sealed record AttackListRecord(int Slot, int Sector, int Opponent, List<int> Targets);

internal sealed partial class NewGameSession
{
    private readonly List<AttackListRecord> _attackLists = [];

    // RULE-ATTACK-002: the probe calls the Attack picker's roster builder for every other player and
    // every living gang of the first human, with the gang's sector, as the picker calls it, and
    // reads the entries it fills. The calls overwrite the target entries and draw the target cards
    // over memory the dump holds, which is why they come after it. The builder tests the
    // visible_to entry of active_player (FND-ATTACK-006), so the calls run with the first human
    // active.
    private bool RecordAttackLists()
    {
        var human = FirstHuman;
        var calls = new Queue<InjectedCall>();
        foreach (var (slot, _, sector) in HumanGangs(human).ToList())
            for (var opponent = 0; opponent < 6; opponent++)
            {
                if (opponent == human) continue;
                var record = new AttackListRecord(slot, sector, opponent, []);
                calls.Enqueue(new InjectedCall(OriginalAddresses.AttackTargetBuilder, [opponent, sector], () =>
                {
                    var entries = _process.Read(OriginalAddresses.AttackTargetEntries, 4 * OriginalAddresses.AttackTargetLength);
                    record.Targets.AddRange(Enumerable.Range(0, OriginalAddresses.AttackTargetLength)
                        .Select(index => BitConverter.ToInt32(entries, 4 * index)).Where(target => target != -1));
                    // FND-ATTACK-006: nothing compares the builder's count with 6, so a full list may
                    // have run past the six entries read here.
                    if (record.Targets.Count == OriginalAddresses.AttackTargetLength)
                        _notes.Add($"Attack list of slot {record.Slot} opponent {record.Opponent} fills all {record.Targets.Count} entries and may be longer.");
                    _attackLists.Add(record);
                }));
            }
        if (calls.Count == 0)
        {
            _notes.Add($"Attack lists: player {human} has no living gang, so no list was built.");
            return true;
        }
        return RunInjectedCalls(calls, human);
    }
}

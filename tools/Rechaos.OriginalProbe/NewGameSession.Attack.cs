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
    // reads the entries it fills. The calls draw the target cards and change nothing the fixture
    // holds, which is why they come after the dump.
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
                    _attackLists.Add(record);
                }));
            }
        return RunInjectedCalls(calls);
    }
}

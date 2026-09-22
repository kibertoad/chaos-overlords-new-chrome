using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Protocol;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The versions that gate a stored match, pinned to the state-fingerprint encoding they gate.
/// </summary>
/// <remarks>
/// <para>
/// A stored fingerprint is compared, never parsed, and nothing in a save, a journal or a stored
/// session records which encoding produced it. One written under an older
/// <see cref="MatchStateHasher"/> encoding is therefore not malformed — it is well-formed and
/// simply fails to match, which reads as damage rather than as an older file. The only thing
/// standing between such a file and that mismatch is the version gate in front of it.
/// </para>
/// <para>
/// When a gate does not move with the encoding the failure is silent and destructive. A save
/// passes its gate, fails verification as a plain <see cref="System.IO.InvalidDataException"/>
/// that <see cref="IncompatibleSave"/> does not recognise, and so counts as damage: the save
/// browser has already drawn the row as playable, the backup generation is consulted and judged
/// not worth keeping, and the next save overwrites it. A journal passes its gate and is then
/// reported as a divergence on its first step, and a resume silently drops it.
/// </para>
/// <para>
/// So these numbers move together, and this test is the tripwire that says so. A failure here is
/// not noise to silence by editing the literal that moved: decide, for each version below, whether
/// the change reaches it — <see cref="MatchStateHasher.FormatVersion"/> for the encoding itself,
/// the save and replay formats for the files that carry a fingerprint, and the session version for
/// the online matches a stored fingerprint keeps resumable — then pin the new set here. The rule
/// is written up in AGENTS.md.
/// </para>
/// </remarks>
public sealed class StateFingerprintVersionCouplingTests
{
    [Fact]
    public void EveryVersionGatingAStoredFingerprintIsPinnedToItsEncoding()
    {
        Assert.Equal(
            (StateHash: 2, NativeSave: 27, Replay: 31, Session: 6),
            (StateHash: MatchStateHasher.FormatVersion,
                NativeSave: NativeSaveSerializer.CurrentFormatVersion,
                Replay: MatchReplaySerializer.CurrentFormatVersion,
                Session: MultiplayerSessionVersion.Current));
    }
}

using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class AudioRoutingTests
{
    [Theory]
    [InlineData((short)0, "SND00500.wav")]
    [InlineData((short)9, "SND00509.wav")]
    [InlineData((short)18, "SND00518.wav")]
    public void WeaponSoundIndexMapsToExtractedBank(short index, string expected) =>
        Assert.Equal(expected, AudioRouting.SoundFile(index));

    [Theory]
    [InlineData((short)-1)]
    [InlineData((short)19)]
    public void WeaponSoundIndexRejectsMissingBankEntries(short index) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioRouting.SoundFile(index));

    [Fact]
    public void ResolvedEquippedWeaponAttackUsesDefinitionSound()
    {
        var data = BundledOriginalData.Load();
        var weapon = data.Items.First(item => item.Type is >= 0 and <= 2 && item.Sound > 0);
        var setupPlayer = new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human);
        var gang = new MatchGangState(
            new GangId(10), setupPlayer.Id, definitionId: 1, sectorId: 0, force: 10,
            weaponItemId: weapon.Id);
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 5),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 5)
            ]))
            .ToArray();
        var state = new MatchState(data,
            new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1, [setupPlayer]),
            [new MatchPlayerState(setupPlayer, 20, [gang])], sectors);
        var gameEvent = new GameEvent(
            0, 1, TurnPhase.Execution, ExecutionPhase.Combat,
            GameEventKind.CommandResolved, setupPlayer.Id, gang.Id, GangAction.Attack,
            CommandTarget.Gang(new GangId(20)),
            Resolution: new CommandResolutionDetails(
                CommandResolutionCode.Resolved, [], 0, ItemId: weapon.Id));

        gang.WeaponItemId = null;

        Assert.Equal(weapon.Sound, AudioRouting.WeaponSound(state, gameEvent));
        Assert.Null(AudioRouting.WeaponSound(state,
            gameEvent with
            {
                Resolution = new CommandResolutionDetails(CommandResolutionCode.TargetEvaded, [], 0)
            }));
    }
}

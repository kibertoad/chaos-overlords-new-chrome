using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class AudioRoutingTests
{
    [Fact]
    public void GeneralSoundSlotsPreserveRecoveredLoaderTable()
    {
        Assert.Equal(
            [
                GeneralSoundSlot.PanelOpen,
                GeneralSoundSlot.PanelClose,
                GeneralSoundSlot.ButtonPress,
                GeneralSoundSlot.AcceptedSelection,
                GeneralSoundSlot.RejectedInput,
                GeneralSoundSlot.IncomingMessageAlert,
                GeneralSoundSlot.CountdownWarning,
                GeneralSoundSlot.FinalSecondWarning,
                GeneralSoundSlot.LoadedWithoutCallSite
            ],
            AudioRouting.GeneralSoundSlots);
        Assert.Equal("SND00200.wav", AudioRouting.GeneralSoundFile(GeneralSoundSlot.PanelOpen));
        Assert.Equal("SND00202.wav", AudioRouting.GeneralSoundFile(GeneralSoundSlot.ButtonPress));
        Assert.Equal("SND00204.wav", AudioRouting.GeneralSoundFile(GeneralSoundSlot.RejectedInput));
        Assert.Equal("SND00205.wav",
            AudioRouting.GeneralSoundFile(GeneralSoundSlot.IncomingMessageAlert));
        Assert.Equal(GeneralSoundSlot.IncomingMessageAlert,
            AudioRouting.IncomingMessageSound(hasUnread: true));
        Assert.Null(AudioRouting.IncomingMessageSound(hasUnread: false));
        Assert.Equal("SND00208.wav",
            AudioRouting.GeneralSoundFile(GeneralSoundSlot.LoadedWithoutCallSite));
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioRouting.GeneralSoundFile(5));
        Assert.Equal(GeneralSoundSlot.AcceptedSelection,
            AudioRouting.PlayerCountResultSound(changed: true, pointerButton: false));
        Assert.Equal(GeneralSoundSlot.RejectedInput,
            AudioRouting.PlayerCountResultSound(changed: false, pointerButton: false));
        Assert.Null(AudioRouting.PlayerCountResultSound(changed: true, pointerButton: true));
        Assert.Equal(GeneralSoundSlot.RejectedInput,
            AudioRouting.PlayerCountResultSound(changed: false, pointerButton: true));
        Assert.Null(AudioRouting.InputResultSound(accepted: true));
        Assert.Equal(GeneralSoundSlot.RejectedInput,
            AudioRouting.InputResultSound(accepted: false));
    }

    [Fact]
    public void PanelSoundsFollowSlidePreferenceAndScreenTransitions()
    {
        Assert.Equal([GeneralSoundSlot.PanelOpen],
            AudioRouting.PanelTransitionSounds(ClientScreen.City, ClientScreen.Gang, true));
        Assert.Equal([GeneralSoundSlot.PanelClose],
            AudioRouting.PanelTransitionSounds(ClientScreen.Gang, ClientScreen.City, true));
        Assert.Equal([GeneralSoundSlot.PanelClose, GeneralSoundSlot.PanelOpen],
            AudioRouting.PanelTransitionSounds(ClientScreen.Commands, ClientScreen.Site, true));
        Assert.Empty(AudioRouting.PanelTransitionSounds(ClientScreen.City, ClientScreen.Sector, false));
        Assert.Empty(AudioRouting.PanelTransitionSounds(ClientScreen.City, ClientScreen.Handoff, true));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 32000)]
    [InlineData(6, 38400)]
    [InlineData(10, 64000)]
    public void RecoveredEffectVolumeMatchesOriginalStereoChannelValue(
        int level, int originalChannelValue) =>
        Assert.Equal(originalChannelValue / (float)ushort.MaxValue,
            AudioRouting.EffectVolumeForLevel(level));

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

        Assert.Equal(weapon.Sound, AudioRouting.CombatSound(state, gameEvent));
        Assert.Null(AudioRouting.CombatSound(state,
            gameEvent with
            {
                Resolution = new CommandResolutionDetails(CommandResolutionCode.TargetEvaded, [], 0)
            }));
    }

    [Fact]
    public void UnarmedCombatUsesMartialArtsPresenceToSelectOriginalSound()
    {
        var data = BundledOriginalData.Load();
        var ordinary = data.Gangs.First(value => value.Stats.MartialArts == 0);
        var martialArtist = data.Gangs.First(value => value.Stats.MartialArts > 0);

        Assert.Equal(AudioRouting.UnarmedSound,
            AudioRouting.CombatSound(StateForGang(data, ordinary.Id),
                UnarmedAttackEvent()));
        Assert.Equal(AudioRouting.MartialArtsSound,
            AudioRouting.CombatSound(StateForGang(data, martialArtist.Id),
                UnarmedAttackEvent()));
    }

    [Fact]
    public void DetectedPoliceUseFixedOriginalSoundAndEvasionIsSilent()
    {
        var state = StateForGang(BundledOriginalData.Load(), 0);
        var detected = PoliceEvent(detected: true);

        Assert.Equal(AudioRouting.PoliceSound, AudioRouting.CombatSound(state, detected));
        Assert.Null(AudioRouting.CombatSound(state, PoliceEvent(detected: false)));
    }

    private static GameEvent UnarmedAttackEvent() => new(
        1, 1, TurnPhase.Execution, ExecutionPhase.Combat,
        GameEventKind.CommandResolved, new PlayerId(0), new GangId(10), GangAction.Attack,
        CommandTarget.Gang(new GangId(20)),
        Resolution: new CommandResolutionDetails(CommandResolutionCode.Resolved, [], 0));

    private static GameEvent PoliceEvent(bool detected) => new(
        1, 1, TurnPhase.Execution, ExecutionPhase.Combat,
        GameEventKind.PoliceAttackResolved, new PlayerId(0), new GangId(10),
        GangAction.None, CommandTarget.Sector(0),
        PoliceAttack: new PoliceAttackResolutionDetails(
            0, 100, 1, detected, 20, 0, [4], detected ? 1 : 0, detected ? 1 : 0, 10, 9));

    private static MatchState StateForGang(OriginalData data, short definitionId)
    {
        var setupPlayer = new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human);
        var gang = new MatchGangState(new GangId(10), setupPlayer.Id, definitionId, 0, 10);
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 5),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 5)
            ]))
            .ToArray();
        return new MatchState(data,
            new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1, [setupPlayer]),
            [new MatchPlayerState(setupPlayer, 20, [gang])], sectors);
    }
}

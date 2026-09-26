using System.Text;
using System.Text.Json.Nodes;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class NativeSaveSerializerTests
{
    [Fact]
    public void RoundTripPreservesCanonicalStateAndQueuedCommandProjection()
    {
        var match = CreateMatch();
        match.AiPlanning.SetCurrentHireRole(new PlayerId(1), 4);
        match.AiPlanning.BeginPlanning(new PlayerId(1));
        match.AiPlanning.SetCurrentHireRole(new PlayerId(1), 2);
        match.AiPlanning.SetFamily(new PlayerId(1), 0, 6);
        match.AiPlanning.SetSectorAnchor(new PlayerId(1), 63);
        match.AiPlanning.SetPlannedAction(
            new PlayerId(1), 0, GangAction.Attack, new AiActionTarget(0, 4));
        match.AiPlanning.RollActiveGangActions(new PlayerId(1), match.Players[1].Gangs);
        match.AiPlanning.SetEquipmentCooldown(new PlayerId(1), 0, EquipmentSlot.Weapon, 12);
        match.AiPlanning.SetEquipmentCooldown(new PlayerId(1), 0, EquipmentSlot.Armor, 15);
        match.AiPlanning.SetFocusValue(new PlayerId(1), 0, 37);
        match.AiPlanning.SetCoverageSector(new PlayerId(1), 0, 23);
        match.FinishUpkeep();
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(0), GangAction.Hide, CommandTarget.None, Repeat: true)).Accepted);
        match.FinishCommand(new PlayerId(0));
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(1), new GangId(1), GangAction.Move, CommandTarget.Sector(62))).Accepted);
        match.FinishCommand(new PlayerId(1));

        var restored = RoundTrip(match);

        Assert.Equal(MatchStateHasher.ComputeFingerprint(match), MatchStateHasher.ComputeFingerprint(restored));
        Assert.Equal(match.Events, restored.Events);
        Assert.Equal(match.NotificationsFor(new PlayerId(0)), restored.NotificationsFor(new PlayerId(0)));
        Assert.Equal(match.PhaseHashes, restored.PhaseHashes);
        Assert.Equal(GangAction.Hide, restored.FindGang(new GangId(0))!.QueuedCommand!.Command.Action);
        Assert.True(restored.FindGang(new GangId(0))!.QueuedCommand!.Command.Repeat);
        Assert.Equal(GangAction.Move, restored.FindGang(new GangId(1))!.QueuedCommand!.Command.Action);
        Assert.Equal(AiDifficulty.CrimeLord, restored.Setup.AiMentality);
        Assert.Equal(7, restored.Setup.Players[1].PortraitId);
        Assert.Equal(match.AiStrategy.CaptureReactions(), restored.AiStrategy.CaptureReactions());
        Assert.Equal(match.AiStrategy.CaptureAttitudes(), restored.AiStrategy.CaptureAttitudes());
        Assert.Equal(2, restored.AiPlanning.CurrentHireRole(new PlayerId(1)));
        Assert.Equal(4, restored.AiPlanning.PreviousHireRole(new PlayerId(1)));
        Assert.Equal(6, restored.AiPlanning.Family(new PlayerId(1), 0));
        Assert.Equal(63, restored.AiPlanning.SectorAnchor(new PlayerId(1)));
        Assert.Equal(GangAction.Attack, restored.AiPlanning.PreviousAction(new PlayerId(1), 0));
        Assert.Equal(new AiActionTarget(0, 4), restored.AiPlanning.PreviousTarget(new PlayerId(1), 0));
        Assert.Equal(GangAction.Move, restored.AiPlanning.PlannedAction(new PlayerId(1), 0));
        Assert.Equal(new AiActionTarget(62, 0), restored.AiPlanning.PlannedTarget(new PlayerId(1), 0));
        Assert.True(restored.AiPlanning.HasPlanned(new PlayerId(1)));
        Assert.Equal(12, restored.AiPlanning.WeaponCooldown(new PlayerId(1), 0));
        Assert.Equal(15, restored.AiPlanning.ArmorCooldown(new PlayerId(1), 0));
        Assert.Equal(37, restored.AiPlanning.FormationSector(new PlayerId(1), 0));
        Assert.Equal(37, restored.AiPlanning.FocusValue(new PlayerId(1), 0));
        Assert.Equal(23, restored.AiPlanning.CoverageSector(new PlayerId(1), 0));
        Assert.Equal(SaveBytes(match), SaveBytes(restored));
    }

    [Fact]
    public void RoundTripPreservesThreeItemSellCommand()
    {
        var match = CreateMatch();
        var gang = match.FindGang(new GangId(0))!;
        var weapon = match.Definitions.Items.First(item => item.Type is >= 0 and <= 2).Id;
        var armor = match.Definitions.Items.First(item => item.Type == 3).Id;
        var miscellaneous = match.Definitions.Items.First(item => item.Type == 4).Id;
        gang.WeaponItemId = weapon;
        gang.ArmorItemId = armor;
        gang.MiscellaneousItemId = miscellaneous;
        match.FinishUpkeep();
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), gang.Id, GangAction.Sell, CommandTarget.Item(weapon),
            SecondaryTarget: CommandTarget.Item(armor),
            TertiaryTarget: CommandTarget.Item(miscellaneous))).Accepted);

        var restored = RoundTrip(match);
        var command = restored.FindGang(gang.Id)!.QueuedCommand!.Command;

        Assert.Equal(CommandTarget.Item(weapon), command.Target);
        Assert.Equal(CommandTarget.Item(armor), command.SecondaryTarget);
        Assert.Equal(CommandTarget.Item(miscellaneous), command.TertiaryTarget);
        Assert.Equal(MatchStateHasher.ComputeFingerprint(match), MatchStateHasher.ComputeFingerprint(restored));
    }

    [Fact]
    public void RoundTripPreservesThreeItemGiveCommand()
    {
        var match = CreateMatch();
        var source = match.FindGang(new GangId(0))!;
        var recipient = new MatchGangState(
            new GangId(50), new PlayerId(0), source.DefinitionId, source.SectorId, 5);
        match.Players[0].AddGang(recipient);
        var weapon = match.Definitions.Items.First(item => item.Type is >= 0 and <= 2).Id;
        var armor = match.Definitions.Items.First(item => item.Type == 3).Id;
        var miscellaneous = match.Definitions.Items.First(item => item.Type == 4).Id;
        source.WeaponItemId = weapon;
        source.ArmorItemId = armor;
        source.MiscellaneousItemId = miscellaneous;
        match.FinishUpkeep();
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), source.Id, GangAction.Give, CommandTarget.Gang(recipient.Id),
            SecondaryTarget: CommandTarget.Item(weapon),
            TertiaryTarget: CommandTarget.Item(armor),
            QuaternaryTarget: CommandTarget.Item(miscellaneous))).Accepted);

        var restored = RoundTrip(match);
        var command = restored.FindGang(source.Id)!.QueuedCommand!.Command;

        Assert.Equal(CommandTarget.Item(weapon), command.SecondaryTarget);
        Assert.Equal(CommandTarget.Item(armor), command.TertiaryTarget);
        Assert.Equal(CommandTarget.Item(miscellaneous), command.QuaternaryTarget);
        Assert.Equal(MatchStateHasher.ComputeFingerprint(match), MatchStateHasher.ComputeFingerprint(restored));
    }

    [Fact]
    public void RoundTripPreservesComlinkMessagesAndReadState()
    {
        var match = CreateMatch(secondPlayerHuman: true);
        match.FinishUpkeep();
        Assert.True(match.SendComlinkMessage(
            new PlayerId(0), [new PlayerId(1)], "MEET ME DOWNTOWN").Accepted);
        Assert.True(match.SendComlinkMessage(
            new PlayerId(0), [new PlayerId(1)], "COME ALONE").Accepted);
        var inbox = match.ComlinkFor(new PlayerId(1));
        match.MarkComlinkRead(new PlayerId(1), inbox.Messages[1].Sequence);

        var restored = RoundTrip(match);

        Assert.Equal(match.ComlinkFor(new PlayerId(1)).Messages,
            restored.ComlinkFor(new PlayerId(1)).Messages);
        Assert.Equal(match.ComlinkFor(new PlayerId(1)).ReadSequences,
            restored.ComlinkFor(new PlayerId(1)).ReadSequences);
        Assert.True(restored.ComlinkFor(new PlayerId(1)).HasUnread);
        Assert.Equal(MatchStateHasher.ComputeFingerprint(match), MatchStateHasher.ComputeFingerprint(restored));
    }








    [Fact]
    public void RoundTripPreservesCrackdownDurationAndDuplicateResetSlots()
    {
        var match = CreateMatch();
        match.Sectors[0].CrackdownActive = true;
        match.Sectors[0].CrackdownTurnsRemaining = 5;
        match.Sectors[0].RecordCrackdown(1);
        match.Sectors[0].RecordCrackdown(3);
        match.Sectors[0].RecordCrackdown(5);

        var restored = RoundTrip(match);

        Assert.True(restored.Sectors[0].CrackdownActive);
        Assert.Equal(5, restored.Sectors[0].CrackdownTurnsRemaining);
        Assert.Equal([5, 5], restored.Sectors[0].CrackdownHistory);
        Assert.Equal(MatchStateHasher.ComputeFingerprint(match), MatchStateHasher.ComputeFingerprint(restored));
    }

    [Fact]
    public void RestoredMatchContinuesWithIdenticalResolutionAndRandomStream()
    {
        var original = CreateMatch();
        original.FinishUpkeep();
        Assert.True(original.Submit(new GameCommand(
            new PlayerId(0), new GangId(0), GangAction.Hide, CommandTarget.None)).Accepted);
        original.FinishCommand(new PlayerId(0));
        original.FinishCommand(new PlayerId(1));
        var restored = RoundTrip(original);

        while (original.Coordinator.Phase == TurnPhase.Execution)
        {
            original.FinishExecutionPhase();
            restored.FinishExecutionPhase();
            Assert.Equal(MatchStateHasher.ComputeFingerprint(original), MatchStateHasher.ComputeFingerprint(restored));
        }
    }

    [Fact]
    public void RoundTripPreservesPendingHireSlotForDeterministicRefill()
    {
        var original = CreateMatch();
        AdvanceToHire(original);
        Assert.True(original.QueueHire(new PlayerId(0), 2, 0).Accepted);
        var restored = RoundTrip(original);

        Assert.Equal(MatchStateHasher.ComputeFingerprint(original), MatchStateHasher.ComputeFingerprint(restored));
        original.FinishHire(new PlayerId(0));
        restored.FinishHire(new PlayerId(0));
        Assert.Equal(MatchStateHasher.ComputeFingerprint(original), MatchStateHasher.ComputeFingerprint(restored));
        Assert.Equal(original.Random.ConsumptionCount, restored.Random.ConsumptionCount);
        Assert.Equal(original.Players[0].HirePool, restored.Players[0].HirePool);
        Assert.Equal(original.Players[0].HireOfferSlots, restored.Players[0].HireOfferSlots);
        var tombstoneRestored = RoundTrip(original);
        Assert.Equal(HireOfferSlotState.Vacant(2),
            tombstoneRestored.Players[0].HireOfferSlots[0]);
        Assert.Equal(MatchStateHasher.ComputeFingerprint(original),
            MatchStateHasher.ComputeFingerprint(tombstoneRestored));
        Assert.Equal(SaveBytes(original), SaveBytes(tombstoneRestored));
    }

    [Fact]
    public void RoundTripPreservesDeferredPendingHirePayment()
    {
        var original = CreateMatch();
        AdvanceToHire(original);
        var cashBefore = original.Players[0].Cash;
        var spentBefore = original.Players[0].Statistics.CashSpent;
        Assert.True(original.QueueHire(new PlayerId(0), 2, 0).Accepted);

        var restored = RoundTrip(original);

        var pending = Assert.Single(restored.Players[0].PendingHires);
        Assert.False(pending.InitialCostPaid);
        Assert.Equal(cashBefore, restored.Players[0].Cash);
        Assert.Equal(spentBefore, restored.Players[0].Statistics.CashSpent);
        restored.FinishHire(new PlayerId(0));
        Assert.Equal(cashBefore - 1, restored.Players[0].Cash);
        Assert.Equal(spentBefore + 1, restored.Players[0].Statistics.CashSpent);
    }

    [Fact]
    public void RoundTripPreservesMaximumHireForceFlag()
    {
        var match = CreateMatch("SMGMILK");
        Assert.True(match.Players[0].UsesMaximumHireForce);

        var restored = RoundTrip(match);

        Assert.True(restored.Players[0].UsesMaximumHireForce);
        Assert.Equal(MatchStateHasher.ComputeFingerprint(match),
            MatchStateHasher.ComputeFingerprint(restored));
        Assert.Equal(SaveBytes(match), SaveBytes(restored));
    }




    [Fact]
    public void RejectsModifiedSectorAnchorWhoseFingerprintWasNotUpdated()
    {
        var match = CreateMatch();
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["runtime"]!["aiPlanning"]!["sectorAnchors"]![0] = 65;

        using var changed = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));

        Assert.Throws<InvalidDataException>(() =>
            NativeSaveSerializer.Load(changed, match.Definitions));
    }

    [Fact]
    public void RejectsModifiedAiActionWhoseFingerprintWasNotUpdated()
    {
        var match = CreateMatch();
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["runtime"]!["aiPlanning"]!["plannedActions"]![81] = (int)GangAction.Attack;

        using var changed = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));

        Assert.Throws<InvalidDataException>(() =>
            NativeSaveSerializer.Load(changed, match.Definitions));
    }

    [Fact]
    public void RejectsModifiedMaximumHireForceFlagWhoseFingerprintWasNotUpdated()
    {
        var match = CreateMatch();
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["players"]![0]!["usesMaximumHireForce"] = true;

        using var changed = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));

        Assert.Throws<InvalidDataException>(() =>
            NativeSaveSerializer.Load(changed, match.Definitions));
    }


    [Fact]
    public void RejectsModifiedPendingHirePaymentMarker()
    {
        var match = CreateMatch();
        AdvanceToHire(match);
        Assert.True(match.QueueHire(new PlayerId(0), 2, 0).Accepted);
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["players"]![0]!["pendingHires"]![0]!["initialCostPaid"] = true;

        using var changed = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));

        Assert.Throws<InvalidDataException>(() =>
            NativeSaveSerializer.Load(changed, match.Definitions));
    }

    [Fact]
    public void RoundTripPreservesPendingSnubSlotAndTombstone()
    {
        var original = CreateMatch();
        AdvanceToHire(original);
        Assert.True(original.SnubHireOffer(new PlayerId(0), 3).Accepted);

        var restored = RoundTrip(original);

        Assert.Equal(1, restored.Players[0].SnubbedHireOfferSlot);
        Assert.Equal(MatchStateHasher.ComputeFingerprint(original),
            MatchStateHasher.ComputeFingerprint(restored));
        original.FinishHire(new PlayerId(0));
        restored.FinishHire(new PlayerId(0));
        Assert.Equal(HireOfferSlotState.Vacant(3),
            restored.Players[0].HireOfferSlots[1]);
        Assert.Equal(MatchStateHasher.ComputeFingerprint(original),
            MatchStateHasher.ComputeFingerprint(restored));
    }

    [Fact]
    public void RejectsUnknownVersionAndMismatchedDefinitions()
    {
        var match = CreateMatch();
        using var stream = new MemoryStream();
        NativeSaveSerializer.Save(stream, match);
        var json = Encoding.UTF8.GetString(stream.ToArray());
        var unknownVersion = json.Replace(
            $"\"formatVersion\":{NativeSaveSerializer.CurrentFormatVersion}",
            "\"formatVersion\":999", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => NativeSaveSerializer.Load(
            new MemoryStream(Encoding.UTF8.GetBytes(unknownVersion)), match.Definitions));

        var changedDefinitions = match.Definitions with
        {
            Sites = match.Definitions.Sites
                .Select((site, index) => index == 0 ? site with { Cash = checked((short)(site.Cash + 1)) } : site)
                .ToArray()
        };
        stream.Position = 0;
        Assert.Throws<InvalidDataException>(() => NativeSaveSerializer.Load(stream, changedDefinitions));
    }










    [Fact]
    public void RejectsModifiedAiStrategyWhoseFingerprintWasNotUpdated()
    {
        var match = CreateMatch();
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        var reactions = document["runtime"]!["aiStrategy"]!["reactions"]!.AsArray();
        reactions[0] = reactions[0]!.GetValue<int>() == 5 ? 4 : 5;

        using var changed = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));

        Assert.Throws<InvalidDataException>(() =>
            NativeSaveSerializer.Load(changed, match.Definitions));
    }

    [Fact]
    public void RejectsModifiedAiPlanningWhoseFingerprintWasNotUpdated()
    {
        var match = CreateMatch();
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["runtime"]!["aiPlanning"]!["currentHireRoles"]![0] = 1;

        using var changed = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));

        Assert.Throws<InvalidDataException>(() =>
            NativeSaveSerializer.Load(changed, match.Definitions));
    }

    [Fact]
    public void RejectsParsedContentWhoseAuthoritativeStateWasModified()
    {
        var match = CreateMatch();
        using var stream = new MemoryStream();
        NativeSaveSerializer.Save(stream, match);
        var json = Encoding.UTF8.GetString(stream.ToArray());
        var changedCash = json.Replace("\"cash\":500", "\"cash\":499", StringComparison.Ordinal);

        Assert.NotEqual(json, changedCash);
        Assert.Throws<InvalidDataException>(() => NativeSaveSerializer.Load(
            new MemoryStream(Encoding.UTF8.GetBytes(changedCash)), match.Definitions));
    }

    [Fact]
    public void RejectsSiteInfluenceThatDoesNotMatchSectorControl()
    {
        var match = CreateMatch();
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["sectors"]![0]!["sites"]![0]!["influencedBy"] = 1;

        using var changed = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));
        var error = Assert.Throws<InvalidDataException>(() =>
            NativeSaveSerializer.Load(changed, match.Definitions));

        var cause = Assert.IsType<ArgumentException>(error.InnerException);
        Assert.Contains("controlling its sector", cause.Message);
    }

    [Fact]
    public void RejectsInputOverExplicitSizeLimit()
    {
        using var oversized = new MemoryStream(new byte[NativeSaveSerializer.MaximumSaveBytes + 1]);

        Assert.Throws<InvalidDataException>(() =>
            NativeSaveSerializer.Load(oversized, BundledOriginalData.Load()));
    }

    [Fact]
    public void AtomicStoreKeepsPreviousSaveAsRecoverableBackup()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rechaos-save-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "match.rchsave");
        try
        {
            var first = CreateMatch();
            NativeSaveStore.SaveAtomic(path, first);
            var firstHash = MatchStateHasher.ComputeFingerprint(first);
            first.FinishUpkeep();
            NativeSaveStore.SaveAtomic(path, first);
            var currentHash = MatchStateHasher.ComputeFingerprint(first);

            Assert.Equal(currentHash, MatchStateHasher.ComputeFingerprint(
                NativeSaveStore.Load(path, first.Definitions)));
            File.WriteAllText(path, "corrupt");
            var recovered = NativeSaveStore.LoadRecoveringBackup(path, first.Definitions);
            Assert.True(recovered.RecoveredFromBackup);
            Assert.True(recovered.PrimaryRepaired);
            Assert.Equal(firstHash, MatchStateHasher.ComputeFingerprint(recovered.State));
            Assert.Equal(firstHash, MatchStateHasher.ComputeFingerprint(
                NativeSaveStore.Load(path, first.Definitions)));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AtomicStoreDoesNotOverwriteGoodBackupWithCorruptCurrentSave()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rechaos-save-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "match.rchsave");
        try
        {
            var match = CreateMatch();
            NativeSaveStore.SaveAtomic(path, match);
            match.FinishUpkeep();
            NativeSaveStore.SaveAtomic(path, match);
            var backupHash = MatchStateHasher.ComputeFingerprint(
                NativeSaveStore.Load(path + NativeSaveStore.BackupSuffix, match.Definitions));
            File.WriteAllText(path, "corrupt");

            match.FinishCommand(new PlayerId(0));
            NativeSaveStore.SaveAtomic(path, match);

            Assert.Equal(MatchStateHasher.ComputeFingerprint(match), MatchStateHasher.ComputeFingerprint(
                NativeSaveStore.Load(path, match.Definitions)));
            Assert.Equal(backupHash, MatchStateHasher.ComputeFingerprint(
                NativeSaveStore.Load(path + NativeSaveStore.BackupSuffix, match.Definitions)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// A sector holding six live gangs plus the records of gangs that died there still saves.
    /// </summary>
    /// <remarks>
    /// An eliminated gang keeps the sector it died in — the hire resolver reuses the record in
    /// place — and every rule that enforces the six-gang bound counts only active gangs. A long
    /// match therefore accumulates more records than the bound in a contested sector, and counting
    /// them made the save the game had just written refuse to load.
    /// </remarks>
    [Fact]
    public void RoundTripAcceptsInactiveGangRecordsBeyondSectorCapacity()
    {
        var match = CreateMatch();
        var player = match.Players[1];
        // A gang joining after the match is built carries its values, as a hire does (RULE-GANG-001).
        var statistics = EffectiveStatistics.From(match.Definitions.Gang(4).Stats);
        foreach (var index in Enumerable.Range(0, MatchLimits.FriendlyGangsPerSector))
            player.AddGang(new MatchGangState(new GangId(30 + index), player.Id, 4, 62, 5,
                statistics: statistics));
        foreach (var index in Enumerable.Range(0, 3))
            player.AddGang(new MatchGangState(new GangId(40 + index), player.Id, 4, 62, 0,
                statistics: statistics));

        var restored = RoundTrip(match);

        Assert.Equal(MatchStateHasher.ComputeFingerprint(match), MatchStateHasher.ComputeFingerprint(restored));
        Assert.Equal(MatchLimits.FriendlyGangsPerSector, restored.Players[1].Gangs
            .Count(gang => gang.IsActive && gang.SectorId == 62));
        Assert.Equal(3, restored.Players[1].Gangs.Count(gang => !gang.IsActive && gang.SectorId == 62));
    }

    /// <summary>A seventh LIVE gang in one sector is still a state no save may carry.</summary>
    [Fact]
    public void RoundTripStillRefusesMoreActiveGangsThanOneSectorHolds()
    {
        var match = CreateMatch();
        var player = match.Players[1];
        foreach (var index in Enumerable.Range(0, MatchLimits.FriendlyGangsPerSector))
            player.AddGang(new MatchGangState(new GangId(30 + index), player.Id, 4, 62, 5));
        var bytes = SaveBytes(match);
        player.AddGang(new MatchGangState(new GangId(39), player.Id, 4, 62, 5));

        Assert.NotEmpty(bytes);
        var failure = Assert.Throws<InvalidDataException>(() => RoundTrip(match));
        Assert.Contains("capacity", failure.InnerException!.Message, StringComparison.Ordinal);
    }

    private static MatchState RoundTrip(MatchState match)
    {
        using var stream = new MemoryStream(SaveBytes(match));
        stream.Position = 0;
        return NativeSaveSerializer.Load(stream, match.Definitions);
    }

    private static byte[] SaveBytes(MatchState match)
    {
        using var stream = new MemoryStream();
        NativeSaveSerializer.Save(stream, match);
        return stream.ToArray();
    }

    private static void AdvanceToHire(MatchState match)
    {
        match.FinishUpkeep();
        match.FinishCommand(new PlayerId(0));
        match.FinishCommand(new PlayerId(1));
        while (match.Coordinator.Phase == TurnPhase.Execution) match.FinishExecutionPhase();
    }

    private static MatchState CreateMatch(
        string firstPlayerName = "ONE",
        bool secondPlayerHuman = false)
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] playerSetups =
        [
            new(new PlayerId(0), firstPlayerName, PlayerController.Human),
            new(new PlayerId(1), "TWO",
                secondPlayerHuman ? PlayerController.Human : PlayerController.Computer,
                PortraitId: 7)
        ];
        var setup = new MatchSetup(
            ScenarioId.Greed, GameDuration.SixMonths, 1996, playerSetups, AiDifficulty.CrimeLord);
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, id is 0 or 63 ? MatchBootstrap.HeadquartersDefinitionId : (short)0,
                    id is 0 or 63 ? 0 : 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], isImportant: id is 0 or 63))
            .ToArray();
        MatchPlayerStart[] starts =
        [
            new(new PlayerId(0), 0, 10, 500, [2, 3, 4]),
            new(new PlayerId(1), 63, 9, 500, [5, 6, 7])
        ];
        return MatchBootstrap.Create(data, setup, sectors, starts);
    }
}

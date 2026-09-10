using System.Text;
using System.Text.Json.Nodes;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed class NativeSaveSerializerTests
{
    [Fact]
    public void RoundTripPreservesCanonicalStateAndQueuedCommandProjection()
    {
        var match = CreateMatch();
        match.AiPlanning.SetCurrentHireRole(new PlayerId(1), 4);
        match.AiPlanning.BeginPlanning(new PlayerId(1));
        match.AiPlanning.SetCurrentHireRole(new PlayerId(1), 2);
        match.AiPlanning.SetFamily(new PlayerId(1), 0, 6);
        match.FinishUpkeep();
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(0), GangAction.Hide, CommandTarget.None, Repeat: true)).Accepted);
        match.FinishCommand(new PlayerId(0));
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(1), new GangId(1), GangAction.Move, CommandTarget.Sector(62))).Accepted);
        match.FinishCommand(new PlayerId(1));

        var restored = RoundTrip(match);

        Assert.Equal(MatchStateHasher.ComputeSha256(match), MatchStateHasher.ComputeSha256(restored));
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
        Assert.Equal(SaveBytes(match), SaveBytes(restored));
    }

    [Fact]
    public void RoundTripPreservesCrackdownDuration()
    {
        var match = CreateMatch();
        match.Sectors[0].CrackdownActive = true;
        match.Sectors[0].CrackdownTurnsRemaining = 5;
        match.Sectors[0].RecordCrackdown(1);
        match.Sectors[0].RecordCrackdown(3);

        var restored = RoundTrip(match);

        Assert.True(restored.Sectors[0].CrackdownActive);
        Assert.Equal(5, restored.Sectors[0].CrackdownTurnsRemaining);
        Assert.Equal([1, 3], restored.Sectors[0].CrackdownHistory);
        Assert.Equal(MatchStateHasher.ComputeSha256(match), MatchStateHasher.ComputeSha256(restored));
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
            Assert.Equal(MatchStateHasher.ComputeSha256(original), MatchStateHasher.ComputeSha256(restored));
        }
    }

    [Fact]
    public void RoundTripPreservesPendingHireSlotForDeterministicRefill()
    {
        var original = CreateMatch();
        AdvanceToHire(original);
        Assert.True(original.QueueHire(new PlayerId(0), 2, 0).Accepted);
        var restored = RoundTrip(original);

        Assert.Equal(MatchStateHasher.ComputeSha256(original), MatchStateHasher.ComputeSha256(restored));
        original.FinishHire(new PlayerId(0));
        restored.FinishHire(new PlayerId(0));
        Assert.Equal(MatchStateHasher.ComputeSha256(original), MatchStateHasher.ComputeSha256(restored));
        Assert.Equal(original.Random.ConsumptionCount, restored.Random.ConsumptionCount);
        Assert.Equal(original.Players[0].HirePool, restored.Players[0].HirePool);
        Assert.Equal(original.Players[0].HireOfferSlots, restored.Players[0].HireOfferSlots);
        var tombstoneRestored = RoundTrip(original);
        Assert.Equal(HireOfferSlotState.Vacant(2),
            tombstoneRestored.Players[0].HireOfferSlots[0]);
        Assert.Equal(MatchStateHasher.ComputeSha256(original),
            MatchStateHasher.ComputeSha256(tombstoneRestored));
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
        Assert.Equal(MatchStateHasher.ComputeSha256(match),
            MatchStateHasher.ComputeSha256(restored));
        Assert.Equal(SaveBytes(match), SaveBytes(restored));
    }

    [Fact]
    public void VersionNineSaveMigratesMaximumHireForceFlagToFalse()
    {
        var match = CreateMatch("SMGMILK");
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["formatVersion"] = 9;
        document["stateSha256"] = MatchStateHasher.ComputeVersionTwelveSha256(match);
        foreach (var player in document["players"]!.AsArray())
            player!.AsObject().Remove("usesMaximumHireForce");

        using var legacy = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));
        var restored = NativeSaveSerializer.Load(legacy, match.Definitions);

        Assert.False(restored.Players[0].UsesMaximumHireForce);
        Assert.Equal(MatchStateHasher.ComputeVersionTwelveSha256(match),
            MatchStateHasher.ComputeVersionTwelveSha256(restored));
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
    public void VersionEightPaidPendingHireMigratesWithoutDoublePayment()
    {
        var match = CreateMatch();
        AdvanceToHire(match);
        Assert.True(match.QueueHireLegacyImmediatePayment(new PlayerId(0), 2, 0).Accepted);
        var paidCash = match.Players[0].Cash;
        var paidSpent = match.Players[0].Statistics.CashSpent;
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["formatVersion"] = 8;
        document["stateSha256"] = MatchStateHasher.ComputeVersionElevenSha256(match);
        document["players"]![0]!["pendingHires"]![0]!.AsObject().Remove("initialCostPaid");

        using var legacy = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));
        var restored = NativeSaveSerializer.Load(legacy, match.Definitions);

        Assert.True(Assert.Single(restored.Players[0].PendingHires).InitialCostPaid);
        var rerestored = RoundTrip(restored);
        Assert.Equal(MatchStateHasher.ComputeSha256(restored), MatchStateHasher.ComputeSha256(rerestored));
        Assert.True(Assert.Single(rerestored.Players[0].PendingHires).InitialCostPaid);
        rerestored.FinishHire(new PlayerId(0));
        Assert.Equal(paidCash, rerestored.Players[0].Cash);
        Assert.Equal(paidSpent, rerestored.Players[0].Statistics.CashSpent);
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
        Assert.Equal(MatchStateHasher.ComputeSha256(original),
            MatchStateHasher.ComputeSha256(restored));
        original.FinishHire(new PlayerId(0));
        restored.FinishHire(new PlayerId(0));
        Assert.Equal(HireOfferSlotState.Vacant(3),
            restored.Players[0].HireOfferSlots[1]);
        Assert.Equal(MatchStateHasher.ComputeSha256(original),
            MatchStateHasher.ComputeSha256(restored));
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
    public void VersionOneSaveMigratesSiteCashDerivedSectorIncome()
    {
        var match = CreateMatch();
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["formatVersion"] = 1;
        document["stateSha256"] = MatchStateHasher.ComputeLegacySha256(match);
        foreach (var sector in document["sectors"]!.AsArray())
            sector!.AsObject().Remove("income");

        using var legacy = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));
        var restored = NativeSaveSerializer.Load(legacy, match.Definitions);

        Assert.All(restored.Sectors, sector => Assert.Equal(
            sector.Sites.Sum(site => match.Definitions.Sites.Single(
                definition => definition.Id == site.DefinitionId).Cash),
            sector.Income));
    }

    [Fact]
    public void VersionTwoSaveMigratesActiveCrackdownToMinimumDuration()
    {
        var match = CreateMatch();
        match.Sectors[0].CrackdownActive = true;
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["formatVersion"] = 2;
        document["stateSha256"] = MatchStateHasher.ComputeVersionTwoSha256(match);
        foreach (var sector in document["sectors"]!.AsArray())
        {
            sector!.AsObject().Remove("crackdownTurnsRemaining");
            sector.AsObject().Remove("crackdownHistory");
        }

        using var legacy = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));
        var restored = NativeSaveSerializer.Load(legacy, match.Definitions);

        Assert.Equal(ManualRules.MinimumCrackdownTurns, restored.Sectors[0].CrackdownTurnsRemaining);
        Assert.Empty(restored.Sectors[0].CrackdownHistory);
    }

    [Fact]
    public void VersionThreeSavePreservesDurationAndStartsWithEmptyHistory()
    {
        var match = CreateMatch();
        match.Sectors[0].CrackdownActive = true;
        match.Sectors[0].CrackdownTurnsRemaining = 5;
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["formatVersion"] = 3;
        document["stateSha256"] = MatchStateHasher.ComputeVersionThreeSha256(match);
        foreach (var sector in document["sectors"]!.AsArray())
            sector!.AsObject().Remove("crackdownHistory");

        using var legacy = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));
        var restored = NativeSaveSerializer.Load(legacy, match.Definitions);

        Assert.Equal(5, restored.Sectors[0].CrackdownTurnsRemaining);
        Assert.Empty(restored.Sectors[0].CrackdownHistory);
    }

    [Fact]
    public void VersionFourSaveMigratesMissingDifficultyToCriminal()
    {
        var match = CreateMatch();
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["formatVersion"] = 4;
        document["stateSha256"] = MatchStateHasher.ComputeVersionFourSha256(match);
        foreach (var player in document["setup"]!["players"]!.AsArray())
            player!.AsObject().Remove("difficulty");

        using var legacy = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));
        var restored = NativeSaveSerializer.Load(legacy, match.Definitions);

        Assert.Equal(AiDifficulty.Criminal, restored.Setup.AiMentality);
        Assert.Equal([0, 1], restored.Setup.Players.Select(player => (int)player.PortraitId));
    }

    [Fact]
    public void VersionFiveSaveReconstructsAiStrategyWithoutAdvancingSavedRandomStream()
    {
        var match = CreateMatch();
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["formatVersion"] = 5;
        document["stateSha256"] = MatchStateHasher.ComputeVersionFiveSha256(match);
        document["runtime"]!.AsObject().Remove("aiStrategy");

        using var legacy = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));
        var restored = NativeSaveSerializer.Load(legacy, match.Definitions);

        Assert.Equal(match.Random.State, restored.Random.State);
        Assert.Equal(match.Random.ConsumptionCount, restored.Random.ConsumptionCount);
        Assert.Equal(match.AiStrategy.CaptureReactions(), restored.AiStrategy.CaptureReactions());
        Assert.Equal(match.AiStrategy.CaptureAttitudes(), restored.AiStrategy.CaptureAttitudes());
        Assert.Equal(MatchStateHasher.ComputeSha256(match), MatchStateHasher.ComputeSha256(restored));
    }

    [Fact]
    public void VersionSixSaveMigratesEmptyAiPlanningState()
    {
        var match = CreateMatch();
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["formatVersion"] = 6;
        document["stateSha256"] = MatchStateHasher.ComputeVersionSixSha256(match);
        document["runtime"]!.AsObject().Remove("aiPlanning");

        using var legacy = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));
        var restored = NativeSaveSerializer.Load(legacy, match.Definitions);

        Assert.Equal(0, restored.AiPlanning.CurrentHireRole(new PlayerId(0)));
        Assert.Equal(0, restored.AiPlanning.PreviousHireRole(new PlayerId(0)));
        Assert.All(restored.AiPlanning.CaptureFamilies(),
            family => Assert.Equal(AiPlanningState.UnusedFamily, family));
        Assert.Equal(MatchStateHasher.ComputeSha256(match), MatchStateHasher.ComputeSha256(restored));
    }

    [Fact]
    public void VersionSevenSaveMigratesCompactHirePoolIntoFixedSlots()
    {
        var match = CreateMatch();
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["formatVersion"] = 7;
        document["stateSha256"] = MatchStateHasher.ComputeVersionTenSha256(match);
        foreach (var player in document["players"]!.AsArray())
        {
            player!.AsObject().Remove("hireOfferSlots");
            player.AsObject().Remove("snubbedHireOfferSlot");
        }

        using var legacy = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));
        var restored = NativeSaveSerializer.Load(legacy, match.Definitions);

        Assert.Equal(
            match.Players[0].HirePool.Select(HireOfferSlotState.Available),
            restored.Players[0].HireOfferSlots);
    }

    [Fact]
    public void VersionSevenMidHireMigrationPreservesPrematureReplacementWithoutNewRng()
    {
        var match = CreateMatch();
        AdvanceToHire(match);
        Assert.True(match.QueueHireLegacyImmediatePayment(new PlayerId(0), 2, 0).Accepted);
        match.Players[0].SetHireOfferSlot(0,
            new HireOfferSlotState(2, null, LegacyReplacementDefinitionId: 8));
        var legacyHash = MatchStateHasher.ComputeVersionTenSha256(match);
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["formatVersion"] = 7;
        document["stateSha256"] = legacyHash;
        var player = document["players"]![0]!.AsObject();
        player["hirePool"] = JsonNode.Parse("[3,4,8]");
        player.Remove("hireOfferSlots");
        player.Remove("snubbedHireOfferSlot");
        player["pendingHires"]![0]!.AsObject().Remove("offerSlot");

        using var legacy = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));
        var restored = NativeSaveSerializer.Load(legacy, match.Definitions);
        var randomBefore = restored.Random.ConsumptionCount;

        restored.FinishHire(new PlayerId(0));

        Assert.Equal([3, 4, 8], restored.Players[0].HirePool);
        Assert.Equal(randomBefore + 3, restored.Random.ConsumptionCount);
    }

    [Fact]
    public void VersionSevenMigrationPreservesLegacySimultaneousHireAndSnub()
    {
        var match = CreateMatch();
        AdvanceToHire(match);
        Assert.True(match.QueueHireLegacyImmediatePayment(new PlayerId(0), 2, 0).Accepted);
        match.Players[0].MarkHireOfferSnubbed(3, 1);
        var legacyHash = MatchStateHasher.ComputeVersionTenSha256(match);
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["formatVersion"] = 7;
        document["stateSha256"] = legacyHash;
        var player = document["players"]![0]!.AsObject();
        player["hirePool"] = JsonNode.Parse("[4]");
        player.Remove("hireOfferSlots");
        player.Remove("snubbedHireOfferSlot");
        player["pendingHires"]![0]!.AsObject().Remove("offerSlot");

        using var legacy = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));
        var restored = NativeSaveSerializer.Load(legacy, match.Definitions);

        Assert.Single(restored.Players[0].PendingHires);
        Assert.Equal((short)3, restored.Players[0].SnubbedHireOffer);
        restored.FinishHire(new PlayerId(0));
        Assert.Equal(HireOfferSlotState.Vacant(2), restored.Players[0].HireOfferSlots[1]);
        Assert.Equal(HireOfferSlotState.Vacant(3), restored.Players[0].HireOfferSlots[2]);
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
            var firstHash = MatchStateHasher.ComputeSha256(first);
            first.FinishUpkeep();
            NativeSaveStore.SaveAtomic(path, first);
            var currentHash = MatchStateHasher.ComputeSha256(first);

            Assert.Equal(currentHash, MatchStateHasher.ComputeSha256(
                NativeSaveStore.Load(path, first.Definitions)));
            File.WriteAllText(path, "corrupt");
            var recovered = NativeSaveStore.LoadRecoveringBackup(path, first.Definitions);
            Assert.True(recovered.RecoveredFromBackup);
            Assert.Equal(firstHash, MatchStateHasher.ComputeSha256(recovered.State));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
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

    private static MatchState CreateMatch(string firstPlayerName = "ONE")
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] playerSetups =
        [
            new(new PlayerId(0), firstPlayerName, PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Computer, PortraitId: 7)
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

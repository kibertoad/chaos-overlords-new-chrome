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
    public void RoundTripPreservesPendingHireAndSnubForDeterministicRefill()
    {
        var original = CreateMatch();
        AdvanceToHire(original);
        Assert.True(original.QueueHire(new PlayerId(0), 2, 0).Accepted);
        Assert.True(original.SnubHireOffer(new PlayerId(0), 3).Accepted);
        var restored = RoundTrip(original);

        Assert.Equal(MatchStateHasher.ComputeSha256(original), MatchStateHasher.ComputeSha256(restored));
        original.FinishHire(new PlayerId(0));
        restored.FinishHire(new PlayerId(0));
        Assert.Equal(MatchStateHasher.ComputeSha256(original), MatchStateHasher.ComputeSha256(restored));
        Assert.Equal(original.Random.ConsumptionCount, restored.Random.ConsumptionCount);
        Assert.Equal(original.Players[0].HirePool, restored.Players[0].HirePool);
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

    private static MatchState CreateMatch()
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] playerSetups =
        [
            new(new PlayerId(0), "ONE", PlayerController.Human),
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

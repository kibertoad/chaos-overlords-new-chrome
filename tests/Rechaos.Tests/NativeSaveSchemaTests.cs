using System.Text;
using System.Text.Json.Nodes;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class NativeSaveSerializerTests
{
    [Fact]
    public void CurrentSaveRejectsOutcomeThatDisagreesWithEndedEvent()
    {
        var match = CreateMatch();
        match.Players[1].Status = PlayerStatus.Eliminated;
        match.FinishUpkeep();
        match.FinishCommand(new PlayerId(0));
        match.FinishCommand(new PlayerId(1));
        while (match.Coordinator.Phase == TurnPhase.Execution)
            match.FinishExecutionPhase();
        match.FinishHire(new PlayerId(0));
        match.FinishHire(new PlayerId(1));
        match.FinishPlayerElimination();
        Assert.NotNull(match.Outcome);
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["runtime"]!["outcome"]!["turn"] = 2;
        using var modified = new MemoryStream(
            Encoding.UTF8.GetBytes(document.ToJsonString()));

        var exception = Assert.Throws<InvalidDataException>(() =>
            NativeSaveSerializer.Load(modified, match.Definitions));

        Assert.Contains("match outcome is invalid", exception.InnerException!.Message);
    }

    [Fact]
    public void CurrentSaveRejectsInvalidNotificationShape()
    {
        var match = CreateMatch();
        ResolveSecondUpkeep(match);
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["runtime"]!["notifications"]![0]!["items"]![0]!["executionPhase"] = 0;
        using var modified = new MemoryStream(
            Encoding.UTF8.GetBytes(document.ToJsonString()));

        var exception = Assert.Throws<InvalidDataException>(() =>
            NativeSaveSerializer.Load(modified, match.Definitions));

        Assert.Contains("notification history is invalid", exception.InnerException!.Message);
    }

    [Fact]
    public void CurrentSaveRejectsNotificationFromFutureTurn()
    {
        var match = CreateMatch();
        ResolveSecondUpkeep(match);
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["runtime"]!["notifications"]![0]!["items"]![0]!["turn"] = 3;
        using var modified = new MemoryStream(
            Encoding.UTF8.GetBytes(document.ToJsonString()));

        var exception = Assert.Throws<InvalidDataException>(() =>
            NativeSaveSerializer.Load(modified, match.Definitions));

        Assert.Contains("notification history is invalid", exception.InnerException!.Message);
    }

    [Fact]
    public void CurrentSaveRejectsNotificationLinkedToDifferentTurnEvent()
    {
        var match = CreateMatch();
        ResolveSecondUpkeep(match);
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["runtime"]!["notifications"]![0]!["items"]![0]!["relatedEventSequence"] = 2;
        using var modified = new MemoryStream(
            Encoding.UTF8.GetBytes(document.ToJsonString()));

        var exception = Assert.Throws<InvalidDataException>(() =>
            NativeSaveSerializer.Load(modified, match.Definitions));

        Assert.Contains("notification history is invalid", exception.InnerException!.Message);
    }

    [Fact]
    public void CurrentSaveRejectsNotificationForUnknownGang()
    {
        var match = CreateMatch();
        match.FinishUpkeep();
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(0), GangAction.Hide, CommandTarget.None)).Accepted);
        foreach (var player in match.Players) match.FinishCommand(player.Id);
        match.FinishExecutionPhase();
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["runtime"]!["notifications"]![0]!["items"]![0]!["gang"] = 999;
        using var modified = new MemoryStream(
            Encoding.UTF8.GetBytes(document.ToJsonString()));

        var exception = Assert.Throws<InvalidDataException>(() =>
            NativeSaveSerializer.Load(modified, match.Definitions));

        Assert.Contains("notification history is invalid", exception.InnerException!.Message);
    }

    // RULE-COMLINK-007 may drop messages from the front only, so the newest must stay.
    [Fact]
    public void CurrentSaveRejectsComlinkSequenceHoles()
    {
        var match = CreateMatch(secondPlayerHuman: true);
        match.FinishUpkeep();
        Assert.True(match.SendComlinkMessage(
            new PlayerId(0), [new PlayerId(1)], "FIRST").Accepted);
        Assert.True(match.SendComlinkMessage(
            new PlayerId(0), [new PlayerId(1)], "SECOND").Accepted);
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["runtime"]!["comlink"]![1]!["items"]!.AsArray().RemoveAt(1);
        using var modified = new MemoryStream(
            Encoding.UTF8.GetBytes(document.ToJsonString()));

        var exception = Assert.Throws<InvalidDataException>(() =>
            NativeSaveSerializer.Load(modified, match.Definitions));

        Assert.Contains("Comlink inbox is invalid", exception.InnerException!.Message);
    }

    [Fact]
    public void CurrentSaveRejectsReadSequenceOutsideComlinkInbox()
    {
        var match = CreateMatch(secondPlayerHuman: true);
        match.FinishUpkeep();
        Assert.True(match.SendComlinkMessage(
            new PlayerId(0), [new PlayerId(1)], "FIRST").Accepted);
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["runtime"]!["comlink"]![1]!["readSequences"]!.AsArray().Add(99);
        using var modified = new MemoryStream(
            Encoding.UTF8.GetBytes(document.ToJsonString()));

        var exception = Assert.Throws<InvalidDataException>(() =>
            NativeSaveSerializer.Load(modified, match.Definitions));

        Assert.Contains("Comlink inbox is invalid", exception.InnerException!.Message);
    }

    [Fact]
    public void CurrentSaveRequiresPerRecordComlinkReadState()
    {
        var match = CreateMatch();
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["runtime"]!["comlink"]![0]!.AsObject().Remove("readSequences");
        using var modified = new MemoryStream(
            Encoding.UTF8.GetBytes(document.ToJsonString()));

        var exception = Assert.Throws<InvalidDataException>(() =>
            NativeSaveSerializer.Load(modified, match.Definitions));

        Assert.Contains("Comlink read flags are missing", exception.Message);
    }

    [Fact]
    public void CurrentSaveRejectsModifiedPhaseHashHistory()
    {
        var match = CreateMatch();
        match.FinishUpkeep();
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["runtime"]!["phaseHashes"]![0]!["fingerprint"] = new string('0', 32);
        using var modified = new MemoryStream(
            Encoding.UTF8.GetBytes(document.ToJsonString()));

        var exception = Assert.Throws<InvalidDataException>(() =>
            NativeSaveSerializer.Load(modified, match.Definitions));

        Assert.Contains("fingerprint does not match", exception.Message);
    }


    [Fact]
    public void CurrentSaveRejectsNoncontiguousEventHistory()
    {
        var match = CreateMatch();
        ResolveSecondUpkeep(match);
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["runtime"]!["events"]![1]!["sequence"] = 2;
        document["runtime"]!["nextEventSequence"] = 3;
        using var modified = new MemoryStream(
            Encoding.UTF8.GetBytes(document.ToJsonString()));

        var exception = Assert.Throws<InvalidDataException>(() =>
            NativeSaveSerializer.Load(modified, match.Definitions));

        Assert.Contains("event history is invalid", exception.InnerException!.Message);
    }


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CurrentSaveRejectsModifiedEventHistory(bool nestedResolution)
    {
        var match = CreateMatch();
        ResolveSecondUpkeep(match);
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        var gameEvent = document["runtime"]!["events"]![0]!;
        if (nestedResolution)
            gameEvent["economy"]!["resultCash"] = 999;
        else
            gameEvent["player"] = 1;
        using var modified = new MemoryStream(
            Encoding.UTF8.GetBytes(document.ToJsonString()));

        var exception = Assert.Throws<InvalidDataException>(() =>
            NativeSaveSerializer.Load(modified, match.Definitions));

        Assert.Contains("fingerprint does not match", exception.Message);
    }

    private static void ResolveSecondUpkeep(MatchState match)
    {
        match.FinishUpkeep();
        foreach (var player in match.Players) match.FinishCommand(player.Id);
        while (match.Coordinator.Phase == TurnPhase.Execution) match.FinishExecutionPhase();
        foreach (var player in match.Players) match.FinishHire(player.Id);
        match.FinishPlayerElimination();
        match.FinishUpkeep();
    }
}

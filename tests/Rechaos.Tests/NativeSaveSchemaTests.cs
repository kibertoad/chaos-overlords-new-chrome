using System.Text;
using System.Text.Json.Nodes;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class NativeSaveSerializerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CurrentSaveRejectsModifiedEventHistory(bool nestedResolution)
    {
        var match = CreateMatch();
        match.FinishUpkeep();
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        var gameEvent = document["runtime"]!["events"]![0]!;
        if (nestedResolution)
            gameEvent["economy"]!["resultCash"] = 999;
        else
            gameEvent["turn"] = 999;
        using var modified = new MemoryStream(
            Encoding.UTF8.GetBytes(document.ToJsonString()));

        var exception = Assert.Throws<InvalidDataException>(() =>
            NativeSaveSerializer.Load(modified, match.Definitions));

        Assert.Contains("fingerprint does not match", exception.Message);
    }

    [Fact]
    public void VersionNineteenSaveRetainsVersionTwentyTwoHashCompatibility()
    {
        var match = CreateMatch();
        match.FinishUpkeep();
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["formatVersion"] = 19;
        document["stateSha256"] = MatchStateHasher.ComputeVersionTwentyTwoSha256(match);
        using var legacy = new MemoryStream(
            Encoding.UTF8.GetBytes(document.ToJsonString()));

        var restored = NativeSaveSerializer.Load(legacy, match.Definitions);

        Assert.Equal(match.Events, restored.Events);
        Assert.Equal(
            MatchStateHasher.ComputeVersionTwentyTwoSha256(match),
            MatchStateHasher.ComputeVersionTwentyTwoSha256(restored));
    }

    [Theory]
    [InlineData(17, "tertiaryTarget", 18, "commands")]
    [InlineData(18, "quaternaryTarget", 19, "commands")]
    [InlineData(17, "tertiaryTarget", 18, "events")]
    [InlineData(18, "quaternaryTarget", 19, "events")]
    public void LegacySaveRejectsTargetsAddedByLaterSchemas(
        int labeledVersion,
        string property,
        int introducedVersion,
        string collection)
    {
        var match = CreateMatch();
        match.FinishUpkeep();
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(0), GangAction.Hide, CommandTarget.None)).Accepted);
        using var current = new MemoryStream();
        NativeSaveSerializer.Save(current, match);
        var document = JsonNode.Parse(current.ToArray())!.AsObject();
        document["formatVersion"] = labeledVersion;
        document["stateSha256"] = labeledVersion == 17
            ? MatchStateHasher.ComputeVersionTwentySha256(match)
            : MatchStateHasher.ComputeVersionTwentyOneSha256(match);
        var owner = document["runtime"]![collection]![0]!;
        if (collection == "commands") owner = owner["command"]!;
        owner[property] = JsonNode.Parse("{\"kind\":4,\"id\":0}");
        using var legacy = new MemoryStream(
            Encoding.UTF8.GetBytes(document.ToJsonString()));

        var exception = Assert.Throws<InvalidDataException>(() =>
            NativeSaveSerializer.Load(legacy, match.Definitions));

        Assert.Contains($"introduced in format {introducedVersion}", exception.Message);
    }
}

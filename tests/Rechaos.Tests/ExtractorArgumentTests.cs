using Rechaos.Core.Assets;
using Rechaos.Extractor;
using Xunit;

namespace Rechaos.Tests;

/// <summary>The extractor's command line, for the shapes that are a user's mistake.</summary>
public sealed class ExtractorArgumentTests
{
    [Theory]
    [InlineData("--source")]
    [InlineData("--output")]
    [InlineData("--catalog-output")]
    [InlineData("--generate-game-data")]
    public async Task AFlagInLastPlaceWithNoValueIsARefusalRatherThanACrash(string flag)
    {
        var writer = new StringWriter();
        var previous = Console.Error;
        Console.SetError(writer);
        try
        {
            // 2 is "you asked for something that is not a command line"; 1 is an extraction that
            // went wrong, which is what reading past the end of the array used to look like.
            Assert.Equal(2, await ExtractorProgram.RunAsync([flag]));
        }
        finally
        {
            Console.SetError(previous);
        }

        Assert.Contains(flag, writer.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Index", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownFlagIsNamedInTheRefusal()
    {
        var writer = new StringWriter();
        var previous = Console.Error;
        Console.SetError(writer);
        try
        {
            Assert.Equal(2, await ExtractorProgram.RunAsync(["--nonsense"]));
        }
        finally
        {
            Console.SetError(previous);
        }

        Assert.Contains("--nonsense", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnOutputFolderThatCannotBeWrittenHasItsOwnExitCode()
    {
        var root = Path.Combine(Path.GetTempPath(), $"rechaos-extractor-output-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var blocker = Path.Combine(root, "blocker");
        File.WriteAllText(blocker, string.Empty);
        var writer = new StringWriter();
        var previous = Console.Error;
        Console.SetError(writer);
        try
        {
            // The output is checked before the source, so a source that does not exist still
            // gets the output's answer, which the first-start import tells the player apart.
            Assert.Equal(ExtractorExitCodes.OutputNotWritable, await ExtractorProgram.RunAsync(
                ["--source", Path.Combine(root, "missing"), "--output", Path.Combine(blocker, "assets")]));
        }
        finally
        {
            Console.SetError(previous);
            Directory.Delete(root, recursive: true);
        }

        Assert.Contains("Cannot write the asset pack", writer.ToString(), StringComparison.Ordinal);
    }
}

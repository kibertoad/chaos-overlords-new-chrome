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
}

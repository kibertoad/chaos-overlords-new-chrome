using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class StartupFailureReporterTests
{
    [Fact]
    public void StartupMessageExplainsHowToRecoverMissingAssets()
    {
        var message = StartupFailureReporter.BuildUserMessage(
            new FileNotFoundException("Original assets are not installed."),
            Path.Combine("installation", "Game", "Assets"),
            Path.Combine("user", "Logs", "startup-error.log"));

        Assert.Contains("could not start", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Original assets are not installed", message, StringComparison.Ordinal);
        Assert.Contains("Import Assets from Original Chaos Overlords", message, StringComparison.Ordinal);
        Assert.Contains(Path.Combine("installation", "Game", "Assets"), message, StringComparison.Ordinal);
        Assert.Contains("startup-error.log", message, StringComparison.Ordinal);
    }
}

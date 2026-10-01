using Rechaos.Core;
using Rechaos.Game;

// Answers the question a support thread always opens with, without starting a window.
if (args.Contains("--version", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine(GameVersion.Current);
    return 0;
}

if (args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("Rechaos.Game startup check passed.");
    return 0;
}

// Before anything formats a number: see GameCulture for why the player's culture is not used.
GameCulture.Apply();

string? assetRoot = null;
var platformSmokeTest = args.Contains("--platform-smoke-test", StringComparer.OrdinalIgnoreCase);
using var diagnostics = RuntimeDiagnostics.OpenDefault();
UnhandledExceptionEventHandler unhandledException = (_, eventArgs) =>
{
    if (eventArgs.ExceptionObject is Exception exception)
        diagnostics.CaptureCrash(exception, "app-domain");
};
AppDomain.CurrentDomain.UnhandledException += unhandledException;
try
{
    var assetArgument = Array.FindIndex(args, value => value == "--assets");
    assetRoot = assetArgument >= 0 && assetArgument + 1 < args.Length
        ? Path.GetFullPath(args[assetArgument + 1])
        : AssetRootResolver.Resolve(
            AppContext.BaseDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    // --reference-frame <save> <bitmap> [--marker-frame <n>]: show the save as a new game's first
    // planning entry, write the drawing area and exit (ReferenceFrameRequest).
    var referenceArgument = Array.FindIndex(args, value => value == "--reference-frame");
    var markerArgument = Array.FindIndex(args, value => value == "--marker-frame");
    var referenceFrame = referenceArgument >= 0 && referenceArgument + 2 < args.Length
        ? new ReferenceFrameRequest(
            Path.GetFullPath(args[referenceArgument + 1]), Path.GetFullPath(args[referenceArgument + 2]),
            markerArgument >= 0 && markerArgument + 1 < args.Length
                ? int.Parse(args[markerArgument + 1], System.Globalization.CultureInfo.InvariantCulture)
                : null)
        : null;
    using var game = new ChaosGame(
        assetRoot,
        args.Contains("--debug-phases", StringComparer.OrdinalIgnoreCase),
        diagnostics,
        referenceFrame: referenceFrame);
    if (platformSmokeTest)
        return 0;
    try
    {
        game.Run();
    }
    catch (Exception exception)
    {
        // A crash during play is a different event from a crash before the window opened, and
        // sending the player to re-import their assets in the middle of turn 30 helps nobody.
        // The match is still in memory here, so try to get it onto disk before saying anything.
        var recoveryPath = game.TryWriteCrashRecoverySave();
        StartupFailureReporter.ReportMainLoopFailure(exception, recoveryPath, diagnostics);
        return 1;
    }
    return 0;
}
catch (Exception exception)
{
    if (platformSmokeTest)
    {
        Console.Error.WriteLine(exception);
        return 1;
    }
    StartupFailureReporter.Report(exception, assetRoot, diagnostics);
    return 1;
}
finally
{
    AppDomain.CurrentDomain.UnhandledException -= unhandledException;
}

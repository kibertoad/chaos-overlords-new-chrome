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
// --reference-frame <save> <bitmap> [--marker-frame <n>] [--pump-counter <n>] [--selected-sector <n>]
// [--lamps <e>,<c>] [--reference-clicks <x:y[:2]>,...]: show the save at the planning entry it stands at, make the
// clicks, write the drawing area and exit (ReferenceFrameRequest).
ReferenceFrameRequest? referenceFrame;
try
{
    referenceFrame = ReferenceFrameRequest.ParseArguments(args);
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}
using var diagnostics = referenceFrame is null
    ? RuntimeDiagnostics.OpenDefault()
    : RuntimeDiagnostics.Open(Path.Combine(referenceFrame.UserDataDirectory, "Logs"));
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

    // macOS and Linux packages cannot import the assets while they install, so the first start
    // does it (FirstLaunchImport). Windows Setup imports them, and an explicit --assets folder or a
    // run under a test is never offered an import.
    if (assetArgument < 0 && !platformSmokeTest && referenceFrame is null && !OperatingSystem.IsWindows())
    {
        var packState = FirstLaunchImport.Inspect(assetRoot);
        if (packState != AssetPackState.Ready
            && NativeDialogs.TryCreate() is { } dialogs
            && FirstLaunchImport.FindExtractor(AppContext.BaseDirectory) is { } extractor
            && FirstLaunchImport.Run(packState, assetRoot, dialogs,
                source => FirstLaunchImport.RunExtractor(extractor, source, assetRoot))
                == FirstLaunchImportResult.Declined)
            return 0;
    }

    using var game = new ChaosGame(
        assetRoot,
        args.Contains("--debug-phases", StringComparer.OrdinalIgnoreCase),
        diagnostics,
        // DEV-AI-007: local matches started in this session let the computer planner's Moves go
        // to any sector, as the original's do.
        originalComputerMoves: args.Contains("--original-computer-moves", StringComparer.OrdinalIgnoreCase),
        // DEV-AI-008: local matches started in this session let the computer planner's hires go
        // to any sector, as the original's do.
        originalComputerHires: args.Contains("--original-computer-hires", StringComparer.OrdinalIgnoreCase),
        referenceFrame: referenceFrame);
    if (platformSmokeTest)
        return 0;
    try
    {
        game.Run();
    }
    catch (Exception exception) when (referenceFrame is not null)
    {
        // A reference frame runs unattended under a test: a message box would hold the process
        // open until the test's timeout kills it.
        diagnostics.CaptureCrash(exception, "reference-frame");
        Console.Error.WriteLine(exception);
        return 1;
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
    if (platformSmokeTest || referenceFrame is not null)
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

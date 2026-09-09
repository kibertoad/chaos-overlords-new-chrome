using Rechaos.Game;

if (args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("Rechaos.Game startup check passed.");
    return 0;
}

string? assetRoot = null;
var platformSmokeTest = args.Contains("--platform-smoke-test", StringComparer.OrdinalIgnoreCase);
try
{
    var assetArgument = Array.FindIndex(args, value => value == "--assets");
    assetRoot = assetArgument >= 0 && assetArgument + 1 < args.Length
        ? Path.GetFullPath(args[assetArgument + 1])
        : AssetRootResolver.Resolve(
            AppContext.BaseDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    using var game = new ChaosGame(assetRoot, args.Contains("--debug-phases", StringComparer.OrdinalIgnoreCase));
    if (platformSmokeTest)
        return 0;
    game.Run();
    return 0;
}
catch (Exception exception)
{
    if (platformSmokeTest)
    {
        Console.Error.WriteLine(exception);
        return 1;
    }
    StartupFailureReporter.Report(exception, assetRoot);
    return 1;
}

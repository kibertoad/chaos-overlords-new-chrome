using Rechaos.Game;

if (args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("Rechaos.Game startup check passed.");
    return;
}

var assetArgument = Array.FindIndex(args, value => value == "--assets");
var assetRoot = assetArgument >= 0 && assetArgument + 1 < args.Length
    ? Path.GetFullPath(args[assetArgument + 1])
    : AssetRootResolver.Resolve(
        AppContext.BaseDirectory,
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

using var game = new ChaosGame(assetRoot, args.Contains("--debug-phases", StringComparer.OrdinalIgnoreCase));
game.Run();

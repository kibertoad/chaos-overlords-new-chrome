using Rechaos.Game;

var assetArgument = Array.FindIndex(args, value => value == "--assets");
var assetRoot = assetArgument >= 0 && assetArgument + 1 < args.Length
    ? Path.GetFullPath(args[assetArgument + 1])
    : Path.Combine(AppContext.BaseDirectory, "Assets");

using var game = new ChaosGame(assetRoot);
game.Run();

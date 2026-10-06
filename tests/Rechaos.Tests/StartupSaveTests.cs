using System.Reflection;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>RULE-UI-013, FND-PLATFORM-009: a save named on the command line is opened at start.</summary>
public sealed class StartupSaveTests
{
    [Fact]
    public void TheFirstPlainArgumentIsTheSave()
    {
        Assert.Equal(Path.GetFullPath(@"C:\Games\SAVE1.json"), StartupSave.PathFrom([@"C:\Games\SAVE1.json"]));
        Assert.Equal(Path.GetFullPath("save.json"),
            StartupSave.PathFrom(["--original-computer-moves", "save.json"]));
    }

    [Fact]
    public void AnOptionsValueAndAReferenceFrameAreNotTheSave()
    {
        Assert.Null(StartupSave.PathFrom([]));
        Assert.Null(StartupSave.PathFrom(["--assets", @"C:\Assets"]));
        Assert.Null(StartupSave.PathFrom(["--reference-frame", "save.json", "out.bmp"]));
        Assert.Null(ReferenceFrameRequest.ParseArguments([@"C:\Games\SAVE1.json"]));
    }

    [Fact]
    public void AStartThatOpensASaveSkipsTheIntroAndOpensItAfterLoadingAssets()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var load = typeof(ChaosGame).GetMethod("LoadContent", flags)!;
        Assert.Contains(typeof(ChaosGame).GetMethod("OpenStartupSave", flags)!, DeviationBehaviourTests.Calls(load));

        // A start that has loaded a match skips the intro.
        var game = DeviationBehaviourTests.HeadlessGame();
        DeviationBehaviourTests.Field("_state").SetValue(game, NativeSaveSerializerTests.CreateMatch());
        DeviationBehaviourTests.Call(game, "InitializeIntroMovies");
        Assert.False((bool)DeviationBehaviourTests.Field("_introMoviesPlaying").GetValue(game)!);
    }
}

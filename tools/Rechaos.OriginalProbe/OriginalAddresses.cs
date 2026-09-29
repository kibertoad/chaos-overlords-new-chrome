namespace Rechaos.OriginalProbe;

/// <summary>Addresses in BLD-GOG-EN-1.1's executable, each from the spec finding named beside it.</summary>
internal static class OriginalAddresses
{
    public const string ExecutableSha256 = "a1430159bbe20869e277a5000311344f4ec141ab77c96b385336617149e97d89";

    // FND-RNG-001: the runtime srand, called once at process start with the clock.
    public const uint SeedGenerator = 0x00478CC0;

    // FND-RNG-003, RULE-RNG-002: the bounded wrapper roll(n), the only caller of rand.
    public const uint Roll = 0x0045D227;

    // FND-RNG-001: the startup function's call of the preference loader; the instruction after it
    // runs once the options are read.
    public const uint PreferenceLoaderCall = 0x00460D14;

    // FND-OPTIONS-001, FND-OPTIONS-003: pref_full_screen and the copy the display code reads.
    public const uint PrefFullScreen = 0x0048786C;
    public const uint PrefFullScreenCopy = 0x00498354;

    // FND-SETUP-002: the full local setup handler.
    public const uint LocalSetup = 0x0040E0A0;

    // SCR-UI-009: File, New Game.
    public const int NewGameCommand = 0x8101;

    // SCR-SETUP-001: the Begin button, (370, 375) with size 92 by 45.
    public const int BeginX = 370 + 46;
    public const int BeginY = 375 + 22;
}

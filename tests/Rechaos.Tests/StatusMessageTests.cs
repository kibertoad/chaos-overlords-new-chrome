using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class StatusMessageTests
{
    [Fact]
    public void EveryPlayerFacingValidationMessageFitsTheCityStatusLine()
    {
        Assert.All(Enum.GetValues<CommandValidationCode>()
                .Where(code => code != CommandValidationCode.Valid),
            code => AssertFits("Command", code, CommandValidationMessages.For(code)));
        Assert.All(Enum.GetValues<HireValidationCode>(),
            code => AssertFits("Hire", code, HireValidationMessages.For(code)));
        Assert.All(Enum.GetValues<ComlinkValidationCode>(),
            code => AssertFits("Comlink", code, ComlinkValidationMessages.For(code)));
    }

    [Fact]
    public void ErrorPreparationUppercasesAndRejectsOversizedText()
    {
        Assert.Equal("USE OWNED OR OCCUPIED SECTOR.",
            CityStatusMessage.Error("Use owned or occupied sector."));
        Assert.Throws<ArgumentException>(() =>
            CityStatusMessage.Error(new string('X', CityStatusMessage.MaxCharacters + 1)));
    }

    private static void AssertFits<TCode>(string catalog, TCode code, string message) =>
        Assert.True(CityStatusMessage.Fits(message),
            $"{catalog} validation {code} is too long: {message}");
}

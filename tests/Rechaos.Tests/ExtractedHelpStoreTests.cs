using System.Text.Json;
using Rechaos.Core.Assets;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class ExtractedHelpStoreTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("rechaos-help-");

    [Fact]
    public void CurrentBoundedDocumentLoads()
    {
        var expected = Document();
        Write(expected);

        var actual = Assert.IsType<ExtractedHelpDocument>(
            ExtractedHelpStore.LoadOrNull(_directory.FullName));
        Assert.Equal(expected.FormatVersion, actual.FormatVersion);
        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.Topics, actual.Topics);
        Assert.Equal(expected.Contents, actual.Contents);
    }

    [Fact]
    public void MissingCorruptFutureAndDanglingDocumentsAreUnavailable()
    {
        Assert.Null(ExtractedHelpStore.LoadOrNull(_directory.FullName));
        Directory.CreateDirectory(System.IO.Path.Combine(_directory.FullName, "help"));
        File.WriteAllText(HelpPath(), "not-json");
        Assert.Null(ExtractedHelpStore.LoadOrNull(_directory.FullName));
        Write(Document() with { FormatVersion = 2 });
        Assert.Null(ExtractedHelpStore.LoadOrNull(_directory.FullName));
        Write(Document() with
        {
            Contents = [new ExtractedHelpContentsEntry(0, "Broken", 99)]
        });
        Assert.Null(ExtractedHelpStore.LoadOrNull(_directory.FullName));
    }

    [Fact]
    public void HelpTextWrapPreservesWordsParagraphsAndLongTokens()
    {
        Assert.Equal(["ONE TWO", "THREE", "", "ABCDEFG", "HI"],
            HelpTextLayout.Wrap("ONE TWO THREE\n\nABCDEFGHI", 7));
    }

    [Fact]
    public void HelpTopicWindowKeepsSelectionInsideStableVisibleRange()
    {
        Assert.Equal(0, HelpLayout.TopicWindowStart(80, 0));
        Assert.Equal(33, HelpLayout.TopicWindowStart(80, 40));
        Assert.Equal(65, HelpLayout.TopicWindowStart(80, 79));
        Assert.Throws<ArgumentOutOfRangeException>(() => HelpLayout.TopicWindowStart(80, 80));
    }

    [Fact]
    public void HelpTopicWheelScrollIsBoundedAndTracksWheelDirection()
    {
        Assert.Equal(9, HelpLayout.ScrollTopicWindow(80, 10, 120));
        Assert.Equal(11, HelpLayout.ScrollTopicWindow(80, 10, -120));
        Assert.Equal(0, HelpLayout.ScrollTopicWindow(80, 0, 120));
        Assert.Equal(65, HelpLayout.ScrollTopicWindow(80, 65, -120));
        Assert.Equal(-3, HelpLayout.WheelSteps(-360));
    }

    [Fact]
    public void HelpNavigationUsesContentsOrderAndOmitsUnlistedFragments()
    {
        var document = new ExtractedHelpDocument(
            ExtractedHelpDocument.CurrentFormatVersion,
            "Help",
            [
                new ExtractedHelpTopic(10, "First", "A", true),
                new ExtractedHelpTopic(20, "Additional topic", "B", false),
                new ExtractedHelpTopic(30, "Last", "C", true)
            ],
            [
                new ExtractedHelpContentsEntry(1, "Last", 30),
                new ExtractedHelpContentsEntry(1, "First", 10)
            ]);

        Assert.Equal([2, 0], HelpNavigation.TopicOrder(document));
        Assert.Equal([0, 1, 2], HelpNavigation.TopicOrder(document with { Contents = [] }));
    }

    [Theory]
    [InlineData(ClientScreen.GameInfo, "Game Info Screen")]
    [InlineData(ClientScreen.Give, "Give")]
    [InlineData(ClientScreen.Sell, "Sell")]
    [InlineData(ClientScreen.ComlinkView, "Comm Menu")]
    [InlineData(ClientScreen.Events, "Main Control Panel")]
    public void HelpContextUsesSpecificOriginalTopics(ClientScreen screen, string title) =>
        Assert.Equal(title, HelpNavigation.ContextTitle(screen));

    [Fact]
    public void HelpContextLookupIgnoresLegacyEllipsisStyling()
    {
        var document = new ExtractedHelpDocument(
            ExtractedHelpDocument.CurrentFormatVersion,
            "Help",
            [
                new ExtractedHelpTopic(2, "Introduction", "A", true),
                new ExtractedHelpTopic(7, "Give…", "B", true)
            ],
            [
                new ExtractedHelpContentsEntry(1, "Introduction", 2),
                new ExtractedHelpContentsEntry(1, "Give...", 7)
            ]);
        var order = HelpNavigation.TopicOrder(document);

        Assert.Equal(1, HelpNavigation.FindTopicPosition(
            document, order, ClientScreen.GiveTarget));
    }

    public void Dispose() => _directory.Delete(recursive: true);

    private void Write(ExtractedHelpDocument document)
    {
        Directory.CreateDirectory(System.IO.Path.Combine(_directory.FullName, "help"));
        File.WriteAllText(HelpPath(), JsonSerializer.Serialize(document));
    }

    private string HelpPath() => System.IO.Path.Combine(_directory.FullName, "help", "contents.json");

    private static ExtractedHelpDocument Document() => new(
        ExtractedHelpDocument.CurrentFormatVersion,
        "Synthetic Help",
        [new ExtractedHelpTopic(0, "Topic", "Readable text", true)],
        [new ExtractedHelpContentsEntry(0, "Topic", 0)]);
}

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
        Assert.Equal(expected.Topics.Count, actual.Topics.Count);
        for (var index = 0; index < expected.Topics.Count; index++)
        {
            Assert.Equal(expected.Topics[index] with { Runs = null },
                actual.Topics[index] with { Runs = null });
            Assert.Equal(expected.Topics[index].Runs!, actual.Topics[index].Runs!);
        }
        Assert.Equal(expected.Contents, actual.Contents);
        Assert.Equal(expected.Contexts, actual.Contexts);
    }

    [Fact]
    public void MissingCorruptFutureAndDanglingDocumentsAreUnavailable()
    {
        Assert.Null(ExtractedHelpStore.LoadOrNull(_directory.FullName));
        Directory.CreateDirectory(System.IO.Path.Combine(_directory.FullName, "help"));
        File.WriteAllText(HelpPath(), "not-json");
        Assert.Null(ExtractedHelpStore.LoadOrNull(_directory.FullName));
        Write(Document() with { FormatVersion = ExtractedHelpDocument.CurrentFormatVersion + 1 });
        Assert.Null(ExtractedHelpStore.LoadOrNull(_directory.FullName));
        Write(Document() with
        {
            Contents = [new ExtractedHelpContentsEntry(0, "Broken", 99)]
        });
        Assert.Null(ExtractedHelpStore.LoadOrNull(_directory.FullName));
        Write(Document() with
        {
            Contexts = [new ExtractedHelpContext(null, null, null, 0)]
        });
        Assert.Null(ExtractedHelpStore.LoadOrNull(_directory.FullName));
        Write(Document() with
        {
            Topics = [new ExtractedHelpTopic(0, "Topic", "Mismatch", true, 0,
                [new ExtractedHelpTextRun("Different")])]
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
    public void StyledHelpWrapPreservesFormattingAndLinksAcrossLineBreaks()
    {
        const uint target = 0x86ee9810;
        var topic = new ExtractedHelpTopic(0, "Topic", "ONE TWO THREE", true, 0,
        [
            new ExtractedHelpTextRun("ONE ", Bold: true),
            new ExtractedHelpTextRun("TWO THREE", Underline: true, LinkHash: target)
        ]);

        var lines = HelpTextLayout.Wrap(topic, 7);

        Assert.Equal(["ONE TWO", "THREE"], lines.Select(line => line.Text));
        Assert.True(lines[0].Runs[0].Bold);
        Assert.Equal(target, lines[0].Runs[^1].LinkHash);
        Assert.Equal(target, lines[1].Runs[0].LinkHash);
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
                Topic(10, "First", "A", true),
                Topic(20, "Additional topic", "B", false),
                Topic(30, "Last", "C", true)
            ],
            [
                new ExtractedHelpContentsEntry(1, "Last", 30),
                new ExtractedHelpContentsEntry(1, "First", 10)
            ],
            []);

        Assert.Equal([2, 0], HelpNavigation.TopicOrder(document));
        Assert.Equal([0, 1, 2], HelpNavigation.TopicOrder(document with { Contents = [] }));
    }

    [Theory]
    [InlineData(ClientScreen.GameInfo, "Game Info Screen", "GIS")]
    [InlineData(ClientScreen.Give, "Give", "GIVE")]
    [InlineData(ClientScreen.Sell, "Sell", "SELL")]
    [InlineData(ClientScreen.ComlinkView, "Comm Menu", "COMMMENU")]
    [InlineData(ClientScreen.Events, "Main Control Panel", "MCP")]
    [InlineData(ClientScreen.Online, "Introduction", "SETMPG")]
    public void HelpContextUsesSpecificOriginalTopics(
        ClientScreen screen,
        string title,
        string context)
    {
        Assert.Equal(title, HelpNavigation.ContextTitle(screen));
        Assert.Equal(context, HelpNavigation.ContextReference(screen));
    }

    [Fact]
    public void HelpContextReferenceDisambiguatesDuplicateTitles()
    {
        var document = new ExtractedHelpDocument(
            ExtractedHelpDocument.CurrentFormatVersion,
            "Help",
            [
                Topic(10, "Sites", "Control overview", true),
                Topic(20, "Sites", "Site reference", true)
            ],
            [
                new ExtractedHelpContentsEntry(1, "Sites", 10, "SITE"),
                new ExtractedHelpContentsEntry(1, "Sites", 20, "SITES")
            ],
            []);

        Assert.Equal(1, HelpNavigation.FindTopicPosition(
            document, HelpNavigation.TopicOrder(document), ClientScreen.Site));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HelpLinksResolveContextAnchorsInsideListedOrUnlistedTopics(bool popup)
    {
        const uint hash = 0x12345678;
        var document = new ExtractedHelpDocument(
            ExtractedHelpDocument.CurrentFormatVersion,
            "Help",
            [
                Topic(1, "First", "A", true, 0),
                Topic(2, "Definition", "B", false, 100),
                Topic(3, "Last", "C", true, 200)
            ],
            [],
            [new ExtractedHelpContext(null, hash, null, 150)]);

        Assert.Equal(new HelpLinkTarget(1, popup), HelpNavigation.ResolveLink(
            document, new ExtractedHelpTextRun("LINK", LinkHash: hash, Popup: popup)));
    }

    [Fact]
    public void HelpContextLookupIgnoresLegacyEllipsisStyling()
    {
        var document = new ExtractedHelpDocument(
            ExtractedHelpDocument.CurrentFormatVersion,
            "Help",
            [
                Topic(2, "Introduction", "A", true),
                Topic(7, "Give…", "B", true)
            ],
            [
                new ExtractedHelpContentsEntry(1, "Introduction", 2),
                new ExtractedHelpContentsEntry(1, "Give...", 7)
            ],
            []);
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
        [Topic(0, "Topic", "Readable text", true)],
        [new ExtractedHelpContentsEntry(0, "Topic", 0)],
        []);

    private static ExtractedHelpTopic Topic(
        int id,
        string title,
        string text,
        bool listed,
        int offset = 0) =>
        new(id, title, text, listed, offset, [new ExtractedHelpTextRun(text)]);
}

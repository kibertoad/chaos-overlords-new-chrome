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
        // DEV-HELP-001: the viewer reads the help the extractor took from the player's own help file.
        var expected = Document() with
        {
            Topics = [Topic(0, "Topic", "Readable text", true) with
            {
                Paragraphs = [new ExtractedHelpParagraph(
                    [new ExtractedHelpTextRun("Readable text", FontIndex: 0)], 0x1b7e,
                    12, 6, 20, 18, 12, -6, HelpParagraphAlignment.Center,
                    [new ExtractedHelpTabStop(720, 1)], 1, 2, KeepTogether: true)]
            }]
        };
        Write(expected);

        var actual = Assert.IsType<ExtractedHelpDocument>(
            ExtractedHelpStore.LoadOrNull(_directory.FullName));
        Assert.Equal(expected.FormatVersion, actual.FormatVersion);
        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.Topics.Count, actual.Topics.Count);
        for (var index = 0; index < expected.Topics.Count; index++)
        {
            Assert.Equal(expected.Topics[index] with { Paragraphs = [] },
                actual.Topics[index] with { Paragraphs = [] });
            var expectedParagraphs = expected.Topics[index].Paragraphs;
            var actualParagraphs = actual.Topics[index].Paragraphs;
            Assert.Equal(expectedParagraphs.Count, actualParagraphs.Count);
            for (var paragraph = 0; paragraph < expectedParagraphs.Count; paragraph++)
            {
                Assert.Equal(expectedParagraphs[paragraph] with { Runs = [], TabStops = null },
                    actualParagraphs[paragraph] with { Runs = [], TabStops = null });
                Assert.Equal(expectedParagraphs[paragraph].Runs, actualParagraphs[paragraph].Runs);
                Assert.Equal(expectedParagraphs[paragraph].TabStops!,
                    actualParagraphs[paragraph].TabStops!);
            }
        }
        Assert.Equal(expected.Contents, actual.Contents);
        Assert.Equal(expected.Contexts, actual.Contexts);
        Assert.Equal(expected.Fonts, actual.Fonts);
    }

    // The paragraphs are the only stored copy of a topic's text; the plain text is worked out
    // from them when it is read.
    [Fact]
    public void TopicTextIsStoredOnlyInItsParagraphs()
    {
        var topic = Topic(0, "Topic", "First", true) with
        {
            Paragraphs =
            [
                new ExtractedHelpParagraph([new ExtractedHelpTextRun("First")], 0, TabStops: []),
                new ExtractedHelpParagraph([new ExtractedHelpTextRun("Sec"),
                    new ExtractedHelpTextRun("ond", Bold: true)], 0, TabStops: [])
            ]
        };

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(topic));

        Assert.False(json.RootElement.TryGetProperty("Text", out _));
        Assert.False(json.RootElement.TryGetProperty("Runs", out _));
        Assert.Equal("First\nSecond", topic.Text);
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
        Write(Document() with { Topics = [Topic(0, "Topic", "Readable text", true)
            with { Paragraphs = [new ExtractedHelpParagraph(
                [new ExtractedHelpTextRun("Text")], 0x0002, SpaceBeforeUnits: 40_000,
                TabStops: [])] }] });
        Assert.Null(ExtractedHelpStore.LoadOrNull(_directory.FullName));
        Write(Document() with { Fonts = null });
        Assert.Null(ExtractedHelpStore.LoadOrNull(_directory.FullName));
        Write(Document() with { Topics = [Topic(0, "Topic", "Readable text", true)
            with { Paragraphs = [new ExtractedHelpParagraph(
                Enumerable.Range(0, 4097).Select(_ => new ExtractedHelpTextRun("x")).ToArray(),
                0, TabStops: [])] }] });
        Assert.Null(ExtractedHelpStore.LoadOrNull(_directory.FullName));
        Write(Document() with { Topics = [Topic(0, "Topic", "Readable text", true)
            with { Paragraphs = [new ExtractedHelpParagraph(
                [new ExtractedHelpTextRun("Text", LinkHash: 0x1234u)], 0, TabStops: [])] }] });
        Assert.Null(ExtractedHelpStore.LoadOrNull(_directory.FullName));
        Write(Document() with { Topics = [Topic(0, "Topic", "Readable text", true)
            with { Paragraphs = [new ExtractedHelpParagraph(
                [new ExtractedHelpTextRun("Text", FontIndex: 2)], 0, TabStops: [])] }] });
        Assert.Null(ExtractedHelpStore.LoadOrNull(_directory.FullName));
    }

    [Fact]
    public void DocumentWithoutFontTableLoads()
    {
        // The decoder emits no descriptors when a help file has no |FONT stream.
        Write(Document() with { Fonts = [] });

        Assert.NotNull(ExtractedHelpStore.LoadOrNull(_directory.FullName));
    }

    [Fact]
    public void ExecutableNotesAreDrawnForTopicsWithParagraphRecords()
    {
        var document = Document() with
        {
            Topics = [Topic(0, "Bribe", "Readable text", true)],
            Contents = [new ExtractedHelpContentsEntry(0, "Bribe", 0, "BRIBE")]
        };

        var topic = HelpContentAugmentation.AddExecutableNotes(document).Topics
            .Single(candidate => candidate.Id == 0);
        var lines = HelpTextLayout.Wrap(topic, 62).Select(line => line.Text).ToArray();

        Assert.Contains(HelpContentAugmentation.NoteHeading, lines);
        Assert.Contains(lines, line => line.StartsWith("The shipped game charges $3",
            StringComparison.Ordinal));
    }

    // DEV-HELP-003: space after one paragraph and space before the next add up, and a twip
    // distance becomes pixels at 9 pixels per 221.48 twips, the 10-point body line.
    [Theory]
    [InlineData(null, null, 9)]   // no spacing: the next line follows directly
    [InlineData(12, 12, 18)]      // 115 + 115 twips = 9.35 pixels
    [InlineData(12, null, 14)]    // 115 twips = 4.67 pixels
    [InlineData(6, null, 11)]     // 55 twips = 2.23 pixels
    [InlineData(24, 12, 23)]      // 235 + 115 twips = 14.22 pixels
    [InlineData(-12, null, 9)]    // negative space is drawn as none
    public void ParagraphSpacingAddsSpaceAfterToSpaceBefore(int? after, int? before, int top)
    {
        var topic = Topic(0, "Topic", "ONE", true) with
        {
            Paragraphs =
            [
                new ExtractedHelpParagraph([new ExtractedHelpTextRun("ONE")], 0,
                    SpaceBeforeUnits: 24, SpaceAfterUnits: after, TabStops: []),
                new ExtractedHelpParagraph([new ExtractedHelpTextRun("TWO")], 0,
                    SpaceBeforeUnits: before, TabStops: [])
            ]
        };

        var lines = HelpTextLayout.Wrap(topic, 12);

        // The first paragraph's space before is not drawn.
        Assert.Equal([0, top], lines.Select(line => line.Top));
    }

    // DEV-HELP-003: an empty paragraph is one empty line with its own spacing.
    [Fact]
    public void EmptyParagraphIsOneEmptyLine()
    {
        var topic = Topic(0, "Topic", "ONE", true) with
        {
            Paragraphs =
            [
                new ExtractedHelpParagraph([new ExtractedHelpTextRun("ONE")], 0, TabStops: []),
                new ExtractedHelpParagraph([], 0, TabStops: []),
                new ExtractedHelpParagraph([], 0, TabStops: []),
                new ExtractedHelpParagraph([new ExtractedHelpTextRun("TWO")], 0, TabStops: [])
            ]
        };

        var lines = HelpTextLayout.Wrap(topic, 12);

        Assert.Equal(["ONE", "", "", "TWO"], lines.Select(line => line.Text));
        Assert.Equal([0, 9, 18, 27], lines.Select(line => line.Top));
    }

    // DEV-HELP-003: a line spacing is a minimum when positive and an exact pitch when negative,
    // and a pitch below the pixel font's 9-pixel row is drawn as one row.
    [Theory]
    [InlineData(null, 9)]
    [InlineData(20, 9)]    // 195 twips = 7.92 pixels, below one row
    [InlineData(40, 16)]   // 395 twips = 16.05 pixels
    [InlineData(-40, 16)]  // 405 twips = 16.46 pixels
    public void LineSpacingSetsTheLinePitchInsideAParagraph(int? lineSpacing, int pitch)
    {
        var topic = Topic(0, "Topic", "ONE TWO", true) with
        {
            Paragraphs =
            [
                new ExtractedHelpParagraph([new ExtractedHelpTextRun("ONE TWO THREE")], 0,
                    LineSpacingUnits: lineSpacing, TabStops: [])
            ]
        };

        var lines = HelpTextLayout.Wrap(topic, 7);

        Assert.Equal(["ONE TWO", "THREE"], lines.Select(line => line.Text));
        Assert.Equal([0, pitch], lines.Select(line => line.Top));
    }

    [Fact]
    public void ViewportCountsScrollsAndHitTestsLinesByTheirPixelPositions()
    {
        HelpTextLine[] lines =
        [
            new([new ExtractedHelpTextRun("A")], Top: 0),
            new([new ExtractedHelpTextRun("B")], Top: 9),
            new([new ExtractedHelpTextRun("C")], Top: 23),
            new([new ExtractedHelpTextRun("D")], Top: 32)
        ];

        Assert.Equal(2, HelpTextLayout.VisibleLineCount(lines, 0, 27));
        Assert.Equal(3, HelpTextLayout.VisibleLineCount(lines, 0, 32));
        Assert.Equal(2, HelpTextLayout.MaximumStart(lines, 18));
        Assert.Equal(0, HelpTextLayout.MaximumStart(lines, 41));
        Assert.Equal(1, HelpTextLayout.LineAt(lines, 0, 17));
        Assert.Null(HelpTextLayout.LineAt(lines, 0, 18));
        Assert.Equal(2, HelpTextLayout.LineAt(lines, 0, 23));
        Assert.Equal(3, HelpTextLayout.LineAt(lines, 1, 23));
        Assert.Null(HelpTextLayout.LineAt(lines, 0, 41));
    }

    [Fact]
    public void LargeSourceIndentsKeepHalfTheWidthForText()
    {
        var topic = Topic(0, "Topic", "ABCDEFGH", true) with
        {
            Paragraphs =
            [
                new ExtractedHelpParagraph([new ExtractedHelpTextRun("ABCDEFGH")], 0x0030,
                    LeftIndentUnits: 400, RightIndentUnits: 400, TabStops: [])
            ]
        };

        var lines = HelpTextLayout.Wrap(topic, 12);

        Assert.Equal(["ABCDEF", "GH"], lines.Select(line => line.Text));
        Assert.All(lines, line => Assert.Equal(6, line.ColumnOffset));
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
        var topic = Topic(0, "Topic",
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
    public void StyledHelpWrapMergesAdjacentRunsAndSplitsThemMidRun()
    {
        var topic = Topic(0, "Topic",
        [
            new ExtractedHelpTextRun("ABCDE", Bold: true),
            new ExtractedHelpTextRun("FGHIJ", Bold: true),
            new ExtractedHelpTextRun("KLMNOPQ", Italic: true)
        ]);

        var lines = HelpTextLayout.Wrap(topic, 12);

        Assert.Equal(["ABCDEFGHIJKL", "MNOPQ"], lines.Select(line => line.Text));
        Assert.Equal(["ABCDEFGHIJ", "KL"], lines[0].Runs.Select(run => run.Text));
        Assert.True(lines[0].Runs[0].Bold);
        Assert.False(lines[0].Runs[0].Italic);
        Assert.True(lines[0].Runs[1].Italic);
        Assert.False(lines[0].Runs[1].Bold);
        var tail = Assert.Single(lines[1].Runs);
        Assert.Equal("MNOPQ", tail.Text);
        Assert.True(tail.Italic);
    }

    // DEV-HELP-001 draws the paragraph geometry of FND-HELP-006 on the pixel-font grid.
    [Fact]
    public void ParagraphLayoutUsesSourceIndentCenteringAndSpacing()
    {
        var topic = Topic(0, "Topic", "FIRST SECOND\n\nCENTER", true) with
        {
            Paragraphs =
            [
                new ExtractedHelpParagraph([new ExtractedHelpTextRun("FIRST SECOND")],
                    0x0010, LeftIndentUnits: 18, TabStops: []),
                new ExtractedHelpParagraph([new ExtractedHelpTextRun("CENTER")],
                    0x0802, SpaceBeforeUnits: 12,
                    Alignment: HelpParagraphAlignment.Center, TabStops: [])
            ]
        };

        var lines = HelpTextLayout.Wrap(topic, 12);

        // 175 twips of indent is 1.94 cells; 115 twips of space before is 4.67 pixels.
        Assert.Equal(["FIRST", "SECOND", "CENTER"], lines.Select(line => line.Text));
        Assert.Equal([2, 2, 3], lines.Select(line => line.ColumnOffset));
        Assert.Equal([0, 9, 23], lines.Select(line => line.Top));
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

    [Fact]
    public void HelpAddsExecutableNotesInsideMatchingOriginalSubjects()
    {
        var document = new ExtractedHelpDocument(
            ExtractedHelpDocument.CurrentFormatVersion,
            "Help",
            [
                Topic(10, "Attack…", "Original attack text", true),
                Topic(20, "Crackdown", "Original crackdown text", true),
                Topic(30, "Bribe", "Existing unlisted Bribe text", false),
                Topic(40, "Unrelated", "Unchanged text", true),
                Topic(50, "Hire", "Original hire text", true),
                Topic(60, "The Inner Sanctum", "Original finance text", true)
            ],
            [
                new ExtractedHelpContentsEntry(1, "Attack...", 10, "ATTACK"),
                new ExtractedHelpContentsEntry(1, "Crackdown", 20, "CRACKDOWN"),
                new ExtractedHelpContentsEntry(1, "Unrelated", 40, "OTHER"),
                new ExtractedHelpContentsEntry(1, "Hire", 50, "CONTHIRE"),
                new ExtractedHelpContentsEntry(1, "The Inner Sanctum", 60, "TIS")
            ],
            []);

        var augmented = HelpContentAugmentation.AddExecutableNotes(document);

        Assert.Contains("current Force + Combat", augmented.Topics[0].Text,
            StringComparison.Ordinal);
        Assert.Contains("115 - 5 x effective Stealth", augmented.Topics[1].Text,
            StringComparison.Ordinal);
        Assert.Contains("charges $3", augmented.Topics[2].Text, StringComparison.Ordinal);
        Assert.Contains(augmented.Topics, topic =>
            string.Equals(topic.Title, "Hide", StringComparison.OrdinalIgnoreCase)
            && topic.Text.Contains("recurring Hide stays active", StringComparison.Ordinal));
        Assert.True(augmented.Topics[2].ListedInContents);
        Assert.Equal("Unchanged text", augmented.Topics[3].Text);
        Assert.Contains("reserves the recruit", augmented.Topics[4].Text,
            StringComparison.Ordinal);
        Assert.Contains("Upkeep may make cash negative", augmented.Topics[4].Text,
            StringComparison.Ordinal);
        Assert.Contains("Paid commands are checked when their phase resolves", augmented.Topics[5].Text,
            StringComparison.Ordinal);
        Assert.Contains("Bribe costs $3", augmented.Topics[5].Text,
            StringComparison.Ordinal);
        Assert.Contains(augmented.Topics[0].Paragraphs, paragraph =>
            paragraph.Text == HelpContentAugmentation.NoteHeading
            && paragraph.Runs.All(run => run.Bold));
        Assert.Contains(augmented.Contents, entry =>
            entry.TopicId == 30 && entry.ContextName == "BRIBE");
        Assert.Same(augmented, HelpContentAugmentation.AddExecutableNotes(augmented));
    }

    [Fact]
    public void HelpCreatesNamedListedSubjectsWhenRelevantSectionsAreMissing()
    {
        var document = new ExtractedHelpDocument(
            ExtractedHelpDocument.CurrentFormatVersion,
            "Incomplete Help",
            [Topic(10, "Introduction", "Original introduction", true)],
            [new ExtractedHelpContentsEntry(0, "Introduction", 10, "INTRO")],
            []);

        var augmented = HelpContentAugmentation.AddExecutableNotes(document);

        Assert.DoesNotContain(augmented.Topics, topic =>
            topic.Title.Contains("formula", StringComparison.OrdinalIgnoreCase)
            || topic.Title.Contains("recovered rules", StringComparison.OrdinalIgnoreCase));
        foreach (var expected in new[] { "Bribe", "Chaos", "Control", "Heal", "Hide", "Hire", "Research", "The Inner Sanctum", "Crackdown" })
        {
            var topic = Assert.Single(augmented.Topics, topic =>
                string.Equals(topic.Title, expected, StringComparison.OrdinalIgnoreCase));
            Assert.True(topic.ListedInContents);
            Assert.Contains(augmented.Contents, entry =>
                entry.TopicId == topic.Id
                && string.Equals(entry.Label, expected, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(HelpContentAugmentation.NoteHeading, topic.Text,
                StringComparison.Ordinal);
        }
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
        [],
        [new ExtractedHelpFont("Times New Roman", 2, 0, 20, 0, 0, 0, 0, 0, 0)]);

    private static ExtractedHelpTopic Topic(
        int id,
        string title,
        string text,
        bool listed,
        int offset = 0) =>
        new(id, title, listed, offset,
            [new ExtractedHelpParagraph([new ExtractedHelpTextRun(text)], 0, TabStops: [])]);

    private static ExtractedHelpTopic Topic(
        int id,
        string title,
        IReadOnlyList<ExtractedHelpTextRun> runs) =>
        new(id, title, true, 0, [new ExtractedHelpParagraph(runs, 0, TabStops: [])]);
}

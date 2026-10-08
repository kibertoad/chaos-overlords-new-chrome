using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// One of the original's bitmaps the game draws, and the table of all of them. The constructor is
/// private, so a bitmap the game draws is always in <see cref="All"/>, and the player's game
/// decodes all of them before the first frame.
/// </summary>
internal sealed class OriginalBitmap
{
    // Declared first: static fields initialize in the order they are written, and each bitmap
    // below adds itself to this list.
    private static readonly List<OriginalBitmap> AllBitmaps = [];

    private OriginalBitmap(string fileName, bool transparentWhite = false)
    {
        FileName = fileName;
        TransparentWhite = transparentWhite;
        AllBitmaps.Add(this);
    }

    /// <summary>The file's name in the asset pack's <c>images</c> folder.</summary>
    public string FileName { get; }

    /// <summary>Whether the original's white is keyed out of the decoded texture.</summary>
    public bool TransparentWhite { get; }

    /// <summary>Every bitmap the game draws, a combat strip the asset pack lacks included.</summary>
    public static IReadOnlyList<OriginalBitmap> All => AllBitmaps;

    public static readonly OriginalBitmap TitleBackground = new("PX00130.bmp");
    public static readonly OriginalBitmap SetupBackground = new("PX00143.bmp");
    public static readonly OriginalBitmap SetupControls = new("PX00140.bmp");
    public static readonly OriginalBitmap SetupKeyedControls = new("PX00140.bmp", transparentWhite: true);
    public static readonly OriginalBitmap ClassicLobbyBackground = new("PX00144.bmp");
    public static readonly OriginalBitmap CreditsBackground = new("PX00100.bmp");
    public static readonly OriginalBitmap HandoffPanel = new("PX00132.bmp");
    public static readonly OriginalBitmap CityBackground = new("PX00128.bmp");
    public static readonly OriginalBitmap UiSprites = new("PX00129.bmp");
    public static readonly OriginalBitmap UiKeyedSprites = new("PX00129.bmp", transparentWhite: true);
    public static readonly OriginalBitmap SiteMarkerSprites = new("PX00150.bmp", transparentWhite: true);
    public static readonly OriginalBitmap PoliceSprites = new("PX00300.bmp");
    public static readonly OriginalBitmap SitePortraits = new("PX02000.bmp");
    public static readonly OriginalBitmap GangPortraits = new("PX03000.bmp");
    public static readonly OriginalBitmap ItemPortraits = new("PX04999.bmp");
    public static readonly OriginalBitmap GameInfoBackground = new("PX05021.bmp");
    public static readonly OriginalBitmap IdleGangWarningBackground = new("PX05020.bmp");
    public static readonly OriginalBitmap CityFinanceBackground = new("PX05008.bmp");
    public static readonly OriginalBitmap SectorFinanceBackground = new("PX05019.bmp");
    public static readonly OriginalBitmap RankingBackground = new("PX05011.bmp");
    public static readonly OriginalBitmap GangInfoBackground = new("PX05000.bmp");
    public static readonly OriginalBitmap SectorGangsBackground = new("PX05009.bmp");
    public static readonly OriginalBitmap GangDefinitionInfoBackground = new("PX05022.bmp");
    public static readonly OriginalBitmap SiteInfoBackground = new("PX05002.bmp");
    public static readonly OriginalBitmap ItemInfoBackground = new("PX05001.bmp");
    public static readonly OriginalBitmap CombatBackground = new("PX05014.bmp");
    public static readonly OriginalBitmap CombatResultsBackground = new("PX05012.bmp");
    public static readonly OriginalBitmap LastTurnEventsBackground = new("PX05010.bmp");
    public static readonly OriginalBitmap ComlinkViewBackground = new("PX05017.bmp");
    public static readonly OriginalBitmap ComlinkSendBackground = new("PX05018.bmp");
    public static readonly OriginalBitmap HireComparisonBackground = new("PX05016.bmp");
    public static readonly OriginalBitmap InfluenceBackground = new("PX05005.bmp");
    public static readonly OriginalBitmap TargetAcquisitionBackground = new("PX05003.bmp");
    public static readonly OriginalBitmap EquipmentPurchaseBackground = new("PX05004.bmp");
    public static readonly OriginalBitmap EquipmentResearchBackground = new("PX05007.bmp");
    public static readonly OriginalBitmap EquipmentSellBackground = new("PX05013.bmp");
    public static readonly OriginalBitmap EquipmentGiveBackground = new("PX05015.bmp");
    public static readonly OriginalBitmap MovementBackground = new("PX05006.bmp");
    public static readonly OriginalBitmap SiteSearchBackground = new("PX05024.bmp");
    public static readonly OriginalBitmap EndgameBackground = new("PX00200.bmp");
    public static readonly OriginalBitmap EndgameSprites = new("PX00201.bmp");
    public static readonly OriginalBitmap EndgameKeyedSprites = new("PX00201.bmp", transparentWhite: true);
    public static readonly OriginalBitmap VictoryBackground = new("PX00202.bmp");
    public static readonly OriginalBitmap EliminationBackground = new("PX00203.bmp");

    /// <summary>The city map's ownership layers: neutral, then one per player.</summary>
    public static readonly IReadOnlyList<OriginalBitmap?> CityOwnershipLayers =
        [.. Enumerable.Range(0, MatchLimits.PlayerCount + 1).Select(index => new OriginalBitmap($"PX1000{index}.bmp"))];

    /// <summary>The last turn's event artwork by art number. Art 0 is none; art 4 is drawn with its white keyed out.</summary>
    public static readonly IReadOnlyList<OriginalBitmap?> LastTurnEventArtwork =
        [null, .. Enumerable.Range(1, 9).Select(art => new OriginalBitmap($"PX060{art:00}.bmp", art == 4))];

    /// <summary>The rotating item pictures by item ID.</summary>
    public static readonly IReadOnlyList<OriginalBitmap?> ItemRotations =
        [.. Enumerable.Range(0, 53).Select(itemId => new OriginalBitmap($"PX04{itemId:000}.bmp"))];

    /// <summary>Every combat animation strip by file name, whether or not the asset pack has it.</summary>
    public static readonly IReadOnlyDictionary<string, OriginalBitmap> CombatStrips = CreateCombatStrips();

    private static Dictionary<string, OriginalBitmap> CreateCombatStrips()
    {
        var strips = new Dictionary<string, OriginalBitmap>();
        void Add(string fileName) => strips[fileName] = new OriginalBitmap(fileName);
        for (short animation = 0; animation <= 27; animation++)
            Add(CombatAnimationRouting.AttackFile(animation, false));
        for (short animation = 0; animation <= 28; animation++)
            Add(CombatAnimationRouting.AttackFile(animation, true));
        for (short animation = 0; animation <= 19; animation++)
            Add(CombatAnimationRouting.HitFile(animation, false));
        for (short animation = 0; animation <= 20; animation++)
            Add(CombatAnimationRouting.HitFile(animation, true));
        return strips;
    }
}

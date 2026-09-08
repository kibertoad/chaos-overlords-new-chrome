using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class TransactionResolutionTests
{
    [Fact]
    public void EquipPurchasesItemAndReplacesSameSlot()
    {
        var data = BundledOriginalData.Load();
        var item = ResearchedWeapon(data);
        var oldItem = checked((short)Enumerable.Range(0, data.Items.Count)
            .First(index => index != item && data.Items[index].Type is >= 0 and <= 2));
        var match = CreateMatch(cash: 100, actorWeapon: oldItem,
            researchedItems: new HashSet<short> { item });
        EnterCommand(match);
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Equip, CommandTarget.Item(item))).Accepted);
        EnterTransaction(match);
        var cashBefore = match.Players[0].Cash;

        match.FinishExecutionPhase();

        var definition = match.Definitions.Items[item];
        Assert.Equal(item, match.FindGang(new GangId(10))!.WeaponItemId);
        Assert.Equal(cashBefore - definition.Cost, match.Players[0].Cash);
        Assert.Equal(definition.Cost, match.Players[0].Statistics.CashSpent);
        var details = Assert.Single(match.LastPhaseResolutions).Event!.Resolution!;
        Assert.Equal(item, details.ItemId);
        Assert.Equal(oldItem, details.ReplacedItemId);
        Assert.Equal(-definition.Cost, details.CashDelta);
    }

    [Fact]
    public void EquipFailsAtExecutionWhenCashIsInsufficient()
    {
        var data = BundledOriginalData.Load();
        var item = ResearchedWeapon(data);
        var match = CreateMatch(cash: 0, researchedItems: new HashSet<short> { item });
        EnterCommand(match);
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Equip, CommandTarget.Item(item))).Accepted);
        EnterTransaction(match);

        match.FinishExecutionPhase();

        Assert.Null(match.FindGang(new GangId(10))!.WeaponItemId);
        Assert.Equal(0, match.Players[0].Cash);
        Assert.Equal(CommandResolutionCode.InsufficientCash, Assert.Single(match.LastPhaseResolutions).Code);
        Assert.Equal(GameEventKind.CommandFailed, match.LastPhaseResolutions[0].Event!.Kind);
    }

    [Fact]
    public void GiveTransfersItemAndDestroysRecipientsReplacement()
    {
        var data = BundledOriginalData.Load();
        var item = ResearchedWeapon(data);
        var replaced = checked((short)Enumerable.Range(0, data.Items.Count)
            .First(index => index != item && data.Items[index].Type is >= 0 and <= 2));
        var match = CreateMatch(cash: 100, actorWeapon: item, targetWeapon: replaced);
        EnterCommand(match);
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Give,
            CommandTarget.Gang(new GangId(11)), SecondaryTarget: CommandTarget.Item(item))).Accepted);
        EnterTransaction(match);

        match.FinishExecutionPhase();

        Assert.Null(match.FindGang(new GangId(10))!.WeaponItemId);
        Assert.Equal(item, match.FindGang(new GangId(11))!.WeaponItemId);
        var details = Assert.Single(match.LastPhaseResolutions).Event!.Resolution!;
        Assert.Equal(item, details.ItemId);
        Assert.Equal(replaced, details.ReplacedItemId);
    }

    [Fact]
    public void SellRemovesItemAndPaysHalfRoundedDown()
    {
        var data = BundledOriginalData.Load();
        var item = checked((short)Enumerable.Range(0, data.Items.Count)
            .First(index => data.Items[index].Type != 99 && data.Items[index].Cost > 1 && data.Items[index].Cost % 2 == 1));
        var match = CreateMatch(cash: 100, actorItem: item);
        EnterCommand(match);
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Sell, CommandTarget.Item(item))).Accepted);
        EnterTransaction(match);
        var cashBefore = match.Players[0].Cash;

        match.FinishExecutionPhase();

        Assert.Null(EquipmentRules.EquippedItem(
            match.FindGang(new GangId(10))!, EquipmentRules.SlotFor(match.Definitions.Items[item])));
        var proceeds = match.Definitions.Items[item].Cost / 2;
        Assert.Equal(cashBefore + proceeds, match.Players[0].Cash);
        Assert.Equal(proceeds, match.Players[0].Statistics.CashEarned);
        Assert.Equal(proceeds, Assert.Single(match.LastPhaseResolutions).Event!.Resolution!.CashDelta);
    }

    [Fact]
    public void EquipRequiresResearchAndRecipientTechLevel()
    {
        var data = BundledOriginalData.Load();
        var item = checked((short)Enumerable.Range(0, data.Items.Count)
            .OrderByDescending(index => data.Items[index].TechLevel)
            .First(index => data.Items[index].Type != 99 && data.Items[index].ResearchDifficulty > 0));
        var match = CreateMatch(cash: 100, useLowTechGangs: true);
        EnterCommand(match);

        var unresearched = match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Equip, CommandTarget.Item(item)));

        Assert.Equal(CommandValidationCode.ItemNotResearched, unresearched.Validation.Code);

        var researchedMatch = CreateMatch(cash: 100,
            researchedItems: new HashSet<short> { item }, useLowTechGangs: true);
        EnterCommand(researchedMatch);
        var lowTech = researchedMatch.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Equip, CommandTarget.Item(item)));
        Assert.Equal(CommandValidationCode.InsufficientTechLevel, lowTech.Validation.Code);
    }

    [Fact]
    public void GiveAndSellRequireItemToBeEquippedByActor()
    {
        var data = BundledOriginalData.Load();
        var item = ResearchedWeapon(data);
        var match = CreateMatch(cash: 100);
        EnterCommand(match);

        var give = match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Give,
            CommandTarget.Gang(new GangId(11)), SecondaryTarget: CommandTarget.Item(item)));
        var sell = match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Sell, CommandTarget.Item(item)));

        Assert.Equal(CommandValidationCode.ItemNotEquipped, give.Validation.Code);
        Assert.Equal(CommandValidationCode.ItemNotEquipped, sell.Validation.Code);
    }

    [Fact]
    public void EquivalentTransactionsProduceIdenticalStateAndHash()
    {
        var data = BundledOriginalData.Load();
        var item = ResearchedWeapon(data);
        var first = CreateMatch(cash: 100, researchedItems: new HashSet<short> { item });
        var second = CreateMatch(cash: 100, researchedItems: new HashSet<short> { item });
        QueueEquip(first, item);
        QueueEquip(second, item);

        first.FinishExecutionPhase();
        second.FinishExecutionPhase();

        Assert.Equal(first.Players[0].Cash, second.Players[0].Cash);
        Assert.Equal(first.FindGang(new GangId(10))!.WeaponItemId, second.FindGang(new GangId(10))!.WeaponItemId);
        Assert.Equal(first.PhaseHashes[^1].Sha256, second.PhaseHashes[^1].Sha256);
    }

    [Fact]
    public void TerminateRemovesGangAndAllEquipmentDuringMovement()
    {
        var data = BundledOriginalData.Load();
        var weapon = ResearchedWeapon(data);
        var match = CreateMatch(cash: 100, actorWeapon: weapon);
        EnterCommand(match);
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Terminate, CommandTarget.None)).Accepted);
        match.FinishCommand(new PlayerId(0));
        match.FinishCommand(new PlayerId(1));
        match.FinishExecutionPhase();
        match.FinishExecutionPhase();
        match.FinishExecutionPhase();
        match.FinishExecutionPhase();
        Assert.Equal(ExecutionPhase.Movement, match.Coordinator.ExecutionPhase);

        match.FinishExecutionPhase();

        var gang = match.FindGang(new GangId(10))!;
        Assert.Equal(0, gang.Force);
        Assert.False(gang.Hidden);
        Assert.Null(gang.WeaponItemId);
        Assert.Null(gang.ArmorItemId);
        Assert.Null(gang.MiscellaneousItemId);
        Assert.Equal(GameNotificationKind.Elimination,
            match.NotificationsFor(new PlayerId(0))[^1].Kind);
    }

    [Fact]
    public void MatchRejectsNonPositiveInventoryCounts()
    {
        Assert.Throws<ArgumentException>(() => CreateMatch(
            cash: 100,
            inventory: new Dictionary<short, int> { [0] = 0 }));
    }

    private static void QueueEquip(MatchState match, short item)
    {
        EnterCommand(match);
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Equip, CommandTarget.Item(item))).Accepted);
        EnterTransaction(match);
    }

    private static short ResearchedWeapon(OriginalData data) => checked((short)Enumerable.Range(0, data.Items.Count)
        .First(index => data.Items[index].Type is >= 0 and <= 2 && data.Items[index].ResearchDifficulty > 0));

    private static void EnterCommand(MatchState match) => match.FinishUpkeep();

    private static void EnterTransaction(MatchState match)
    {
        match.FinishCommand(new PlayerId(0));
        match.FinishCommand(new PlayerId(1));
        match.FinishExecutionPhase();
        match.FinishExecutionPhase();
        Assert.Equal(ExecutionPhase.Transaction, match.Coordinator.ExecutionPhase);
    }

    private static MatchState CreateMatch(
        int cash,
        short? actorWeapon = null,
        short? targetWeapon = null,
        short? actorItem = null,
        IReadOnlySet<short>? researchedItems = null,
        bool useLowTechGangs = false,
        IReadOnlyDictionary<short, int>? inventory = null)
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "ONE", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Computer)
        ];
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, setups);
        var gangDefinition = useLowTechGangs
            ? data.Gangs.OrderBy(gang => gang.TechLevel).First()
            : data.Gangs.OrderByDescending(gang => gang.TechLevel).First();

        short? actorArmor = null;
        short? actorMiscellaneous = null;
        if (actorItem is { } equipped)
        {
            switch (EquipmentRules.SlotFor(data.Items[equipped]))
            {
                case EquipmentSlot.Weapon: actorWeapon = equipped; break;
                case EquipmentSlot.Armor: actorArmor = equipped; break;
                case EquipmentSlot.Miscellaneous: actorMiscellaneous = equipped; break;
            }
        }

        MatchPlayerState[] players =
        [
            new(setup.Players[0], cash,
            [
                new MatchGangState(new GangId(10), new PlayerId(0), gangDefinition.Id, 0, 5,
                    actorWeapon, actorArmor, actorMiscellaneous),
                new MatchGangState(new GangId(11), new PlayerId(0), gangDefinition.Id, 0, 5,
                    weaponItemId: targetWeapon)
            ], researchedItems: researchedItems, inventory: inventory),
            new(setup.Players[1], 500,
                [new MatchGangState(new GangId(20), new PlayerId(1), gangDefinition.Id, 0, 5)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ]))
            .ToArray();
        return new MatchState(data, setup, players, sectors);
    }
}

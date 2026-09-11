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
        var cashBefore = match.Players[0].Cash;

        match.FinishExecutionPhase();

        Assert.Null(match.FindGang(new GangId(10))!.WeaponItemId);
        Assert.True(cashBefore < 0);
        Assert.Equal(cashBefore, match.Players[0].Cash);
        Assert.Equal(CommandResolutionCode.InsufficientCash, Assert.Single(match.LastPhaseResolutions).Code);
        Assert.Equal(GameEventKind.CommandFailed, match.LastPhaseResolutions[0].Event!.Kind);
    }

    [Fact]
    public void InfluencedLocalFactoryDiscountsEquipmentThirtyPercent()
    {
        var data = BundledOriginalData.Load();
        var item = ResearchedWeapon(data);
        var match = CreateMatch(cash: 100, researchedItems: new HashSet<short> { item },
            influencedFactory: true);
        QueueEquip(match, item);
        var cashBefore = match.Players[0].Cash;

        match.FinishExecutionPhase();

        var expectedCost = data.Items[item].Cost * SpecialSiteRules.FactoryPricePercent / 100;
        Assert.Equal(cashBefore - expectedCost, match.Players[0].Cash);
        Assert.Equal(-expectedCost, Assert.Single(match.LastPhaseResolutions).Event!.Resolution!.CashDelta);
        Assert.Equal(expectedCost, match.Players[0].Statistics.CashSpent);
    }

    [Fact]
    public void FactoryAcquiredDuringInstantPhaseDiscountsSameTurnReplacement()
    {
        var data = BundledOriginalData.Load();
        var item = data.Items.Single(value => value.Name == "KATANA").Id;
        var replaced = data.Items.Single(value => value.Name == "METAL PIPE").Id;
        var match = CreateMatch(
            cash: 100,
            targetWeapon: replaced,
            researchedItems: new HashSet<short> { item },
            availableFactory: true);
        EnterCommand(match);

        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Influence,
            CommandTarget.Site(0))).Accepted);
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(11), GangAction.Equip,
            CommandTarget.Item(item))).Accepted);
        match.FinishCommand(new PlayerId(0));
        match.FinishCommand(new PlayerId(1));

        match.FinishExecutionPhase();

        Assert.Equal(new PlayerId(0), match.FindSite(0)!.InfluencedBy);
        match.FinishExecutionPhase();
        Assert.Equal(ExecutionPhase.Transaction, match.Coordinator.ExecutionPhase);
        var cashBefore = match.Players[0].Cash;

        match.FinishExecutionPhase();

        var expectedCost = data.Items[item].Cost * SpecialSiteRules.FactoryPricePercent / 100;
        Assert.Equal(7, expectedCost);
        Assert.Equal(cashBefore - expectedCost, match.Players[0].Cash);
        Assert.Equal(item, match.FindGang(new GangId(11))!.WeaponItemId);
        var equip = match.LastPhaseResolutions.Single(result =>
            result.Event!.Action == GangAction.Equip).Event!.Resolution!;
        Assert.Equal(replaced, equip.ReplacedItemId);
        Assert.Equal(-expectedCost, equip.CashDelta);
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
    public void GiveTransfersAllThreeSelectedItemsToOneRecipient()
    {
        var data = BundledOriginalData.Load();
        var weapon = data.Items.First(item => item.Type is >= 0 and <= 2).Id;
        var armor = data.Items.First(item => item.Type == 3).Id;
        var miscellaneous = data.Items.First(item => item.Type == 4).Id;
        var match = CreateMatch(cash: 100, actorWeapon: weapon);
        var source = match.FindGang(new GangId(10))!;
        var target = match.FindGang(new GangId(11))!;
        source.ArmorItemId = armor;
        source.MiscellaneousItemId = miscellaneous;
        EnterCommand(match);

        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), source.Id, GangAction.Give, CommandTarget.Gang(target.Id),
            SecondaryTarget: CommandTarget.Item(weapon),
            TertiaryTarget: CommandTarget.Item(armor),
            QuaternaryTarget: CommandTarget.Item(miscellaneous))).Accepted);
        EnterTransaction(match);
        match.FinishExecutionPhase();

        Assert.Null(source.WeaponItemId);
        Assert.Null(source.ArmorItemId);
        Assert.Null(source.MiscellaneousItemId);
        Assert.Equal(weapon, target.WeaponItemId);
        Assert.Equal(armor, target.ArmorItemId);
        Assert.Equal(miscellaneous, target.MiscellaneousItemId);
        Assert.Equal([weapon, armor, miscellaneous],
            Assert.Single(match.LastPhaseResolutions).Event!.Resolution!.ItemIds);
    }

    [Fact]
    public void GivePhaseAllowsTwoGangsToSwapSameSlotEquipment()
    {
        var data = BundledOriginalData.Load();
        var weapons = data.Items.Where(item => item.Type is >= 0 and <= 2).Take(2).Select(item => item.Id).ToArray();
        var match = CreateMatch(cash: 100, actorWeapon: weapons[0], targetWeapon: weapons[1]);
        var first = match.FindGang(new GangId(10))!;
        var second = match.FindGang(new GangId(11))!;
        EnterCommand(match);

        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), first.Id, GangAction.Give, CommandTarget.Gang(second.Id),
            SecondaryTarget: CommandTarget.Item(weapons[0]))).Accepted);
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), second.Id, GangAction.Give, CommandTarget.Gang(first.Id),
            SecondaryTarget: CommandTarget.Item(weapons[1]))).Accepted);
        EnterTransaction(match);
        match.FinishExecutionPhase();

        Assert.Equal(weapons[1], first.WeaponItemId);
        Assert.Equal(weapons[0], second.WeaponItemId);
        Assert.All(match.LastPhaseResolutions, result => Assert.True(result.Succeeded));
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
    public void SellRemovesAllSelectedEquipmentAndPaysCombinedHalfPrices()
    {
        var data = BundledOriginalData.Load();
        var weapon = data.Items.First(item => item.Type is >= 0 and <= 2).Id;
        var armor = data.Items.First(item => item.Type == 3).Id;
        var miscellaneous = data.Items.First(item => item.Type == 4).Id;
        var match = CreateMatch(cash: 100, actorWeapon: weapon);
        var gang = match.FindGang(new GangId(10))!;
        gang.ArmorItemId = armor;
        gang.MiscellaneousItemId = miscellaneous;
        EnterCommand(match);
        var command = new GameCommand(new PlayerId(0), gang.Id, GangAction.Sell,
            CommandTarget.Item(weapon), SecondaryTarget: CommandTarget.Item(armor),
            TertiaryTarget: CommandTarget.Item(miscellaneous));

        Assert.True(match.Submit(command).Accepted);
        EnterTransaction(match);
        var cashBefore = match.Players[0].Cash;
        match.FinishExecutionPhase();

        Assert.Null(gang.WeaponItemId);
        Assert.Null(gang.ArmorItemId);
        Assert.Null(gang.MiscellaneousItemId);
        var proceeds = new[] { weapon, armor, miscellaneous }
            .Sum(item => EquipmentRules.SaleValue(data.Items[item]));
        Assert.Equal(cashBefore + proceeds, match.Players[0].Cash);
        Assert.Equal(proceeds, Assert.Single(match.LastPhaseResolutions).Event!.Resolution!.CashDelta);
    }

    [Fact]
    public void SellRejectsDuplicateOrGappedAdditionalTargets()
    {
        var data = BundledOriginalData.Load();
        var weapon = data.Items.First(item => item.Type is >= 0 and <= 2).Id;
        var armor = data.Items.First(item => item.Type == 3).Id;
        var match = CreateMatch(cash: 100, actorWeapon: weapon);
        match.FindGang(new GangId(10))!.ArmorItemId = armor;
        EnterCommand(match);

        var duplicate = CommandValidator.Validate(match, new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Sell, CommandTarget.Item(weapon),
            SecondaryTarget: CommandTarget.Item(weapon)));
        var gap = CommandValidator.Validate(match, new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Sell, CommandTarget.Item(weapon),
            TertiaryTarget: CommandTarget.Item(armor)));

        Assert.Equal(CommandValidationCode.InvalidTargetKind, duplicate.Code);
        Assert.Equal(CommandValidationCode.InvalidTargetKind, gap.Code);
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
        IReadOnlyDictionary<short, int>? inventory = null,
        bool influencedFactory = false,
        bool availableFactory = false)
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
                new MatchSiteState(0, id == 0 && (influencedFactory || availableFactory) ? (short)15 : (short)0,
                    id == 0 && availableFactory ? 0 : 7,
                    id == 0 && influencedFactory ? new PlayerId(0) : null),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner: id == 0 && (influencedFactory || availableFactory)
                ? new PlayerId(0) : null))
            .ToArray();
        return new MatchState(data, setup, players, sectors);
    }
}

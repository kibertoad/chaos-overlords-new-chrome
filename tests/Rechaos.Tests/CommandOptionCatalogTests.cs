using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class CommandOptionCatalogTests
{
    [Fact]
    public void ProjectionContainsOnlyDeterministicallyOrderedValidCommands()
    {
        var match = CreateMatch();
        var player = new PlayerId(0);
        var gang = match.Players[0].Gangs[0].Id;
        Assert.Empty(CommandOptionCatalog.LegalCommands(match, player, gang));
        match.FinishUpkeep();

        var first = CommandOptionCatalog.LegalCommands(match, player, gang);
        var second = CommandOptionCatalog.LegalCommands(match, player, gang);

        Assert.NotEmpty(first);
        Assert.Equal(first, second);
        Assert.All(first, command => Assert.True(CommandValidator.Validate(match, command).IsValid));
        Assert.Contains(first, command => command.Action == GangAction.Bribe && command.Target == CommandTarget.None);
        Assert.Contains(first, command => command.Action == GangAction.Move
            && command.Target == CommandTarget.Sector(1));
        Assert.Contains(first, command => command.Action == GangAction.Move
            && command.Target == CommandTarget.Sector(9));
        Assert.Contains(first, command => command.Action == GangAction.Influence
            && command.Target.Kind == CommandTargetKind.Site);
        Assert.DoesNotContain(first, command => command.Action == GangAction.Control);
        Assert.Equal(CommandValidationCode.SectorAlreadyControlled,
            CommandValidator.Validate(match,
                new GameCommand(player, gang, GangAction.Control, CommandTarget.None)).Code);
        Assert.Equal(first.OrderBy(command => command.Action).Select(command => command.Action),
            first.Select(command => command.Action));
    }

    /// <summary>
    /// The sector and site candidates are built once for the process because every match has the
    /// same board. This pins that invariant to a real match: were a board ever to carry a
    /// different set of sectors or site slots, the shared lists would have to go back to being
    /// derived from the state.
    /// </summary>
    [Fact]
    public void SectorAndSiteTargetsAreTheWholeBoardAndAreSharedAcrossMatches()
    {
        var match = CreateMatch();
        var targets = new CommandOptionCatalog.TargetLists(match);
        var other = new CommandOptionCatalog.TargetLists(CreateMatch());

        Assert.Equal(
            match.Sectors.Select(sector => CommandTarget.Sector(sector.Id)),
            targets.For(CommandTargetKind.Sector));
        Assert.Equal(
            match.Sectors.SelectMany(sector => sector.Sites.Select(site =>
                CommandTarget.Site(sector.Id * MatchLimits.SitesPerSector + site.Slot))),
            targets.For(CommandTargetKind.Site));
        Assert.Same(targets.For(CommandTargetKind.Sector), other.For(CommandTargetKind.Sector));
        Assert.Same(targets.For(CommandTargetKind.Site), other.For(CommandTargetKind.Site));
    }

    private static MatchState CreateMatch()
    {
        var data = BundledOriginalData.Load();
        var setupPlayer = new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human);
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, [setupPlayer]);
        var definition = data.Gangs.OrderByDescending(gang => gang.TechLevel).First();
        var gang = new MatchGangState(new GangId(10), setupPlayer.Id, definition.Id, 0, 10);
        var researched = data.Items.Where(item => item.Type != 99).Select(item => item.Id).ToHashSet();
        var player = new MatchPlayerState(setupPlayer, 500, [gang], researchedItems: researched);
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, data.Sites[0].Resistance),
                new MatchSiteState(1, 1, data.Sites[1].Resistance),
                new MatchSiteState(2, 2, data.Sites[2].Resistance)
            ], owner: setupPlayer.Id))
            .ToArray();
        return new MatchState(data, setup, [player], sectors);
    }
}

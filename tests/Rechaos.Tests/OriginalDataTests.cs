using Rechaos.Core.Assets;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalDataTests
{
    [Fact]
    public void IdLookupsAnswerFromTheDefinitionSetTheyWereAskedOn()
    {
        var data = BundledOriginalData.Load();

        Assert.Equal(data.Sites[3], data.Site(data.Sites[3].Id));
        Assert.Equal(data.Gangs[28], data.Gang(data.Gangs[28].Id));
    }

    [Fact]
    public void DerivedDefinitionSetsSeeTheirOwnDefinitions()
    {
        var source = BundledOriginalData.Load();
        // Query the source first so any lookup index it caches is in place before the copy.
        var originalCash = source.Site(0).Cash;
        var originalUpkeep = source.Gang(28).Upkeep;

        var sites = source.Sites.ToArray();
        sites[0] = sites[0] with { Cash = checked((short)(originalCash + 1)) };
        var gangs = source.Gangs.ToArray();
        gangs[28] = gangs[28] with { Upkeep = -3 };
        var changed = source with { Sites = sites, Gangs = gangs };

        Assert.Equal(originalCash + 1, changed.Site(0).Cash);
        Assert.Equal(-3, changed.Gang(28).Upkeep);
        Assert.Equal(originalCash, source.Site(0).Cash);
        Assert.Equal(originalUpkeep, source.Gang(28).Upkeep);
    }

    [Fact]
    public void UnknownIdsThrowInsteadOfReturningAnUnrelatedDefinition()
    {
        var data = BundledOriginalData.Load();

        Assert.Throws<InvalidOperationException>(() => data.Site(-1));
        Assert.Throws<InvalidOperationException>(() => data.Gang(short.MaxValue));
    }
}

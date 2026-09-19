namespace Rechaos.Core.Assets;

/// <summary>
/// Validates the structural and semantic invariants of the three decoded
/// original gameplay tables. These are format checks, not balance rules.
/// </summary>
public static class OriginalDataValidator
{
    public const int SiteCount = 22;
    public const int GangCount = 90;
    public const int ItemRecordCount = 64;
    public const int ActualItemCount = 53;
    public const int SentinelItemCount = ItemRecordCount - ActualItemCount;

    public static void Validate(OriginalData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ValidateCount(data.Sites, SiteCount, "site");
        ValidateCount(data.Gangs, GangCount, "gang");
        ValidateCount(data.Items, ItemRecordCount, "item");

        ValidateOrderedIds(data.Sites.Select(site => site.Id), SiteCount, "site");
        ValidateOrderedIds(data.Gangs.Select(gang => gang.Id), GangCount, "gang");
        ValidateRequiredText(data.Sites.Select(site => (site.Name, $"site {site.Id}")));
        ValidateRequiredText(data.Gangs.Select(gang => (gang.Name, $"gang {gang.Id}")));
        ValidateDistinctNames(data.Sites.Select(site => site.Name), "site");
        ValidateDistinctNames(data.Gangs.Select(gang => gang.Name), "gang");

        ValidateSites(data.Sites);
        ValidateItems(data.Items);
    }

    private static void ValidateSites(IReadOnlyList<SiteDefinition> sites)
    {
        var expectedSpecials = new Dictionary<short, short>
        {
            [4] = 2,  // Research Lab
            [8] = 1,  // Science Center
            [15] = 3  // Factory
        };
        foreach (var site in sites)
        {
            var expected = expectedSpecials.GetValueOrDefault(site.Id);
            if (site.Special != expected)
                throw new InvalidDataException(
                    $"Site {site.Id} has special value {site.Special}; expected {expected}.");
            if (site.Frequency < 0)
                throw new InvalidDataException($"Site {site.Id} has a negative generation frequency.");
        }
    }

    private static void ValidateItems(IReadOnlyList<ItemDefinition> items)
    {
        for (var index = 0; index < ActualItemCount; index++)
        {
            var item = items[index];
            if (item.Id != index)
                throw new InvalidDataException(
                    $"Item record {index} has id {item.Id}; expected {index}.");
            if (string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.Description))
                throw new InvalidDataException($"Item {index} is missing required text.");
            if (!IsExpectedItemType(index, item.Type))
                throw new InvalidDataException(
                    $"Item {index} has category {item.Type}, which is invalid for its table range.");

            var isWeapon = item.Type is 0 or 1 or 2;
            if (isWeapon && (item.AttackAnimation is < 0 or > 27
                    || item.HitAnimation is < 0 or > 20
                    || item.Sound is < 0 or > 18))
                throw new InvalidDataException($"Weapon item {index} has an invalid media index.");
            if (!isWeapon && (item.AttackAnimation != 0 || item.HitAnimation != 0 || item.Sound != 0))
                throw new InvalidDataException($"Non-weapon item {index} unexpectedly references combat media.");
            if (item.CombatPortraitFrame is < 0 or >= 15)
                throw new InvalidDataException($"Item {index} has an invalid combat portrait frame.");
        }

        for (var index = ActualItemCount; index < ItemRecordCount; index++)
        {
            var item = items[index];
            if (item.Type != 99 || item.Id != 0 || item.Name.Length != 0
                || item.Description.Length != 0)
                throw new InvalidDataException(
                    $"Item record {index} is not the expected unused type-99 sentinel.");
        }
    }

    private static bool IsExpectedItemType(int index, short type) => index switch
    {
        < 12 => type is 0 or 1,
        < 24 => type == 2,
        < 38 => type == 3,
        _ => type == 4
    };

    private static void ValidateCount<T>(IReadOnlyList<T>? records, int expected, string label)
    {
        if (records is null || records.Count != expected)
            throw new InvalidDataException(
                $"Original {label} table must contain exactly {expected} records.");
    }

    private static void ValidateOrderedIds(IEnumerable<short> ids, int count, string label)
    {
        if (!ids.SequenceEqual(Enumerable.Range(0, count).Select(value => checked((short)value))))
            throw new InvalidDataException(
                $"Original {label} table ids must be ordered from 0 through {count - 1}.");
    }

    private static void ValidateRequiredText(IEnumerable<(string Text, string Label)> values)
    {
        foreach (var (text, label) in values)
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidDataException($"Original {label} is missing its name.");
    }

    private static void ValidateDistinctNames(IEnumerable<string> names, string label)
    {
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Count())
            throw new InvalidDataException($"Original {label} names must be unique.");
    }
}

using Rechaos.Core.Assets;

namespace Rechaos.Core.GameModel;

public sealed class MatchGangState
{
    public MatchGangState(
        GangId id,
        PlayerId owner,
        short definitionId,
        int sectorId,
        int force,
        short? weaponItemId = null,
        short? armorItemId = null,
        short? miscellaneousItemId = null,
        EffectiveStatistics? statistics = null)
    {
        if (sectorId is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));
        if (force is < 0 or > ManualRules.MaximumForce)
            throw new ArgumentOutOfRangeException(nameof(force));
        Id = id;
        Owner = owner;
        DefinitionId = definitionId;
        SectorId = sectorId;
        Force = force;
        WeaponItemId = weaponItemId;
        ArmorItemId = armorItemId;
        MiscellaneousItemId = miscellaneousItemId;
        StoredStatistics = statistics;
    }

    public GangId Id { get; }
    public PlayerId Owner { get; }
    public short DefinitionId { get; }
    public int SectorId { get; internal set; }
    public int Force { get; internal set; }
    public bool Hidden { get; internal set; }
    public bool HiredThisTurn { get; internal set; }
    public short? WeaponItemId { get; internal set; }
    public short? ArmorItemId { get; internal set; }
    public short? MiscellaneousItemId { get; internal set; }
    public QueuedCommand? QueuedCommand { get; internal set; }
    public bool IsActive => Force > 0;

    /// <summary>
    /// The fourteen statistics written by the last rebuild before planning (RULE-GANG-001), with
    /// the weapon skills in Combat (RULE-COMBAT-001), or the definition's values for a gang hired
    /// since. Null only for a gang that has not joined a match.
    /// </summary>
    public EffectiveStatistics? StoredStatistics { get; internal set; }
}

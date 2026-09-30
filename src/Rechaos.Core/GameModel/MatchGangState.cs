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
    /// The Force byte the original's record keeps once the gang is gone (FMT-STATE-001): the
    /// Force at the start of the combat phase less its damage, capped at 10, which can be below 0
    /// (RULE-COMBAT-002), or the Force it had when it was terminated or its player eliminated.
    /// Null while the gang is active. Only the strength test of BUG-AI-007 reads it.
    /// </summary>
    public int? RetiredForce { get; internal set; }

    /// <summary>
    /// The <c>visible_to</c> bytes of the original's record as one bit per observing player slot:
    /// written for an active gang at each planning entry (RULE-DETECT-001) and kept unchanged once
    /// the gang is gone. Only the strength test of BUG-AI-007 reads a retired gang's bits.
    /// </summary>
    public byte VisibilityMask { get; internal set; }

    /// <summary>
    /// The fourteen statistics written by the last rebuild before planning (RULE-GANG-001), with
    /// the weapon skills in Combat (RULE-COMBAT-001), or the definition's values for a gang hired
    /// since. Null only for a gang that has not joined a match.
    /// </summary>
    public EffectiveStatistics? StoredStatistics { get; internal set; }
}

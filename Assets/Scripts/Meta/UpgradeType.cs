namespace Swarmblastrr.Meta
{
    /// <summary>
    /// The permanent upgrades the player can buy between runs. The numeric value of each member is
    /// the index into <see cref="UpgradeCatalog.All"/> and <see cref="SaveData.UpgradeLevels"/>, so
    /// existing values must never be renumbered; new upgrades are appended.
    /// </summary>
    public enum UpgradeType
    {
        /// <summary>How many units the crowd starts a run with.</summary>
        StartingUnits = 0,

        /// <summary>Boss HP removed per player unit the boss consumes.</summary>
        UnitDamage = 1,

        /// <summary>Multiplier applied to every coin reward.</summary>
        CoinIncome = 2,
    }
}

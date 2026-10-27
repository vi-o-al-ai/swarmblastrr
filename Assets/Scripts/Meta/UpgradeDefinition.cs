namespace Swarmblastrr.Meta
{
    /// <summary>
    /// Static tuning for one upgrade: how its cost grows and what value each level grants.
    /// Pure data; the curves are evaluated by <see cref="UpgradeMath"/>.
    /// </summary>
    [System.Serializable]
    public struct UpgradeDefinition
    {
        /// <summary>Which upgrade this definition describes.</summary>
        public UpgradeType Type;

        /// <summary>Human-readable name shown in the shop.</summary>
        public string DisplayName;

        /// <summary>Cost of going from level 0 to level 1.</summary>
        public int BaseCost;

        /// <summary>Geometric cost growth: cost(level) = round(BaseCost * CostGrowth^level).</summary>
        public float CostGrowth;

        /// <summary>Value granted at level 0.</summary>
        public float BaseValue;

        /// <summary>Linear value growth: value(level) = BaseValue + ValuePerLevel * level.</summary>
        public float ValuePerLevel;

        /// <summary>Highest purchasable level; 0 means unlimited.</summary>
        public int MaxLevel;
    }
}

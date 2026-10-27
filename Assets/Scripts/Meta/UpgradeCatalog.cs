using System;

namespace Swarmblastrr.Meta
{
    /// <summary>
    /// The fixed table of every upgrade in the game. <see cref="All"/> is indexed by
    /// <c>(int)UpgradeType</c>, so the table order must follow the enum order exactly.
    /// </summary>
    public static class UpgradeCatalog
    {
        /// <summary>Every upgrade definition, indexed by <c>(int)UpgradeType</c>.</summary>
        public static readonly UpgradeDefinition[] All = Build();

        /// <summary>
        /// Returns the definition for <paramref name="type"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="type"/> is not a member of <see cref="UpgradeType"/>.
        /// </exception>
        public static UpgradeDefinition Get(UpgradeType type)
        {
            int index = (int)type;
            if (index < 0 || index >= All.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(type), type, "No upgrade definition exists for this type.");
            }

            return All[index];
        }

        private static UpgradeDefinition[] Build()
        {
            UpgradeDefinition[] table = new UpgradeDefinition[3];

            table[(int)UpgradeType.StartingUnits] = new UpgradeDefinition
            {
                Type = UpgradeType.StartingUnits,
                DisplayName = "Starting Units",
                BaseCost = 30,
                CostGrowth = 1.25f,
                BaseValue = 10f,
                ValuePerLevel = 1f,
                MaxLevel = 0,
            };

            table[(int)UpgradeType.UnitDamage] = new UpgradeDefinition
            {
                Type = UpgradeType.UnitDamage,
                DisplayName = "Unit Damage",
                BaseCost = 80,
                CostGrowth = 1.5f,
                BaseValue = 1f,
                ValuePerLevel = 1f,
                MaxLevel = 0,
            };

            table[(int)UpgradeType.CoinIncome] = new UpgradeDefinition
            {
                Type = UpgradeType.CoinIncome,
                DisplayName = "Coin Income",
                BaseCost = 50,
                CostGrowth = 1.35f,
                BaseValue = 1.0f,
                ValuePerLevel = 0.1f,
                MaxLevel = 0,
            };

            return table;
        }
    }
}

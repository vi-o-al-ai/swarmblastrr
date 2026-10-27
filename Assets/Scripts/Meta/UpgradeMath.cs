using System;

namespace Swarmblastrr.Meta
{
    /// <summary>
    /// Pure evaluation of upgrade cost and value curves. Uses only <see cref="System.Math"/> so it
    /// is deterministic and testable without a Unity player loop. Rounding is
    /// <see cref="Math.Round(double)"/> (ties to even), the same rule <c>Mathf.RoundToInt</c> applies.
    /// </summary>
    public static class UpgradeMath
    {
        /// <summary>
        /// Coins needed to buy the next level when the upgrade is currently at <paramref name="level"/>:
        /// <c>round(BaseCost * CostGrowth^level)</c>. Negative levels are treated as 0 and the result is
        /// clamped to <c>[0, int.MaxValue]</c> so very high levels never overflow.
        /// </summary>
        public static int CostFor(in UpgradeDefinition def, int level)
        {
            if (level < 0)
            {
                level = 0;
            }

            double cost = def.BaseCost * Math.Pow(def.CostGrowth, level);
            return RoundToIntClamped(cost);
        }

        /// <summary>
        /// Effective value granted at <paramref name="level"/>:
        /// <c>BaseValue + ValuePerLevel * level</c>. Negative levels are treated as 0.
        /// </summary>
        public static float ValueFor(in UpgradeDefinition def, int level)
        {
            if (level < 0)
            {
                level = 0;
            }

            return def.BaseValue + def.ValuePerLevel * level;
        }

        /// <summary>
        /// True when no further level can be bought. A <see cref="UpgradeDefinition.MaxLevel"/> of 0
        /// means the upgrade is unlimited and this always returns false.
        /// </summary>
        public static bool IsMaxed(in UpgradeDefinition def, int level)
        {
            return def.MaxLevel > 0 && level >= def.MaxLevel;
        }

        private static int RoundToIntClamped(double value)
        {
            if (double.IsNaN(value) || value <= 0d)
            {
                return 0;
            }

            if (value >= int.MaxValue)
            {
                return int.MaxValue;
            }

            return (int)Math.Round(value);
        }
    }
}

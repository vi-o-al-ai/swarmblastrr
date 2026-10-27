using System;
using Swarmblastrr.Core;

namespace Swarmblastrr.Meta
{
    /// <summary>
    /// Pure coin-reward formulas. Only <see cref="System.Math"/> is used so the results are
    /// deterministic and testable; a null <see cref="GameConfig"/> falls back to the contract defaults
    /// (1 coin per surviving unit, 50 clear bonus). Rounding is <see cref="Math.Round(double)"/>
    /// (ties to even), the same rule <c>Mathf.RoundToInt</c> applies.
    /// </summary>
    public static class Economy
    {
        private const int DefaultCoinsPerSurvivingUnit = 1;
        private const int DefaultLevelClearBonus = 50;
        private const int CoinsPerLevelIndex = 10;
        private const int LoseBaseReward = 5;

        /// <summary>
        /// Coins for clearing a level:
        /// <c>round((unitsRemaining * CoinsPerSurvivingUnit * rampMultiplier + LevelClearBonus + levelIndex * 10) * incomeMultiplier)</c>.
        /// Negative inputs are clamped to 0 (a ramp multiplier below 1 counts as 1), and the result is
        /// never negative.
        /// </summary>
        public static int WinReward(int unitsRemaining, int rampMultiplier, float incomeMultiplier, int levelIndex, GameConfig config)
        {
            int coinsPerUnit = DefaultCoinsPerSurvivingUnit;
            int clearBonus = DefaultLevelClearBonus;
            if (config != null)
            {
                coinsPerUnit = config.CoinsPerSurvivingUnit;
                clearBonus = config.LevelClearBonus;
            }

            if (unitsRemaining < 0)
            {
                unitsRemaining = 0;
            }

            if (rampMultiplier < 1)
            {
                rampMultiplier = 1;
            }

            if (levelIndex < 0)
            {
                levelIndex = 0;
            }

            double unitCoins = (double)unitsRemaining * coinsPerUnit * rampMultiplier;
            double raw = (unitCoins + clearBonus + (double)levelIndex * CoinsPerLevelIndex) * ClampMultiplier(incomeMultiplier);
            return RoundToIntClamped(raw);
        }

        /// <summary>
        /// Consolation coins for a lost run: <c>round((5 + levelIndex) * incomeMultiplier)</c>, never negative.
        /// </summary>
        public static int LoseReward(int levelIndex, float incomeMultiplier)
        {
            if (levelIndex < 0)
            {
                levelIndex = 0;
            }

            double raw = ((double)LoseBaseReward + levelIndex) * ClampMultiplier(incomeMultiplier);
            return RoundToIntClamped(raw);
        }

        private static double ClampMultiplier(float incomeMultiplier)
        {
            if (float.IsNaN(incomeMultiplier) || incomeMultiplier < 0f)
            {
                return 0d;
            }

            return incomeMultiplier;
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

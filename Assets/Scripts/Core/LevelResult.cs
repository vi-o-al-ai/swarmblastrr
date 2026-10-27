using System;

namespace Swarmblastrr.Core
{
    /// <summary>
    /// Outcome of one run, produced by <see cref="GameManager.Win"/> / <see cref="GameManager.Lose"/>
    /// and delivered to listeners through <see cref="GameEvents.LevelFinished"/>.
    /// </summary>
    [Serializable]
    public struct LevelResult
    {
        /// <summary>True when the crowd reached the reward ramp, false when it was wiped out.</summary>
        public bool Won;

        /// <summary>0-based index of the level that was played.</summary>
        public int LevelIndex;

        /// <summary>Player units alive when the run ended.</summary>
        public int UnitsRemaining;

        /// <summary>Multiplier from the reward ramp, 1 if none was reached.</summary>
        public int RewardMultiplier;

        /// <summary>Coins credited to the player for this run.</summary>
        public int CoinsEarned;
    }
}

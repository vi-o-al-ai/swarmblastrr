namespace Swarmblastrr.Core
{
    /// <summary>
    /// High-level phase of the game loop. Every transition is made by
    /// <see cref="GameManager"/> and broadcast through <see cref="GameEvents.StateChanged"/>.
    /// </summary>
    public enum GameState
    {
        /// <summary>Nothing has been initialised yet.</summary>
        Boot,

        /// <summary>Between runs: the upgrade shop is open and the level is visible behind it.</summary>
        Shop,

        /// <summary>The crowd is running down the lane through gates and enemies.</summary>
        Running,

        /// <summary>The crowd has reached the boss and is draining its HP.</summary>
        BossFight,

        /// <summary>The boss is dead and the crowd is running up the reward ramp.</summary>
        RewardRamp,

        /// <summary>The run ended successfully; the result screen is shown.</summary>
        Won,

        /// <summary>The crowd was wiped out; the result screen is shown.</summary>
        Lost
    }
}

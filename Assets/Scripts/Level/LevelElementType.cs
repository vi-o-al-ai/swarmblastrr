namespace Swarmblastrr.Level
{
    /// <summary>
    /// Kind of thing a <see cref="LevelElementData"/> entry describes. <see cref="LevelBuilder"/>
    /// turns each kind into the matching <c>Swarmblastrr.Track</c> element; a gate pair becomes
    /// two <c>Gate</c> objects, one per half of the lane.
    /// </summary>
    public enum LevelElementType
    {
        /// <summary>Two gates side by side, one per half of the lane.</summary>
        GatePair,

        /// <summary>A hostile crowd standing on the lane.</summary>
        EnemyCrowd,

        /// <summary>The end-of-level boss with an HP number.</summary>
        Boss,

        /// <summary>The multiplier tiles after the boss.</summary>
        RewardRamp
    }
}

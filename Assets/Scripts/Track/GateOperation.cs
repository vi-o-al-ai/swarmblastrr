namespace Swarmblastrr.Track
{
    /// <summary>
    /// The arithmetic a <see cref="Gate"/> applies to the player's unit count when the crowd
    /// runs through it. Core references this enum from <c>GameEvents.GateHit</c>.
    /// </summary>
    public enum GateOperation
    {
        /// <summary>count + value</summary>
        Add,

        /// <summary>count - value</summary>
        Subtract,

        /// <summary>count * value</summary>
        Multiply,

        /// <summary>count / value; a value of zero or less leaves the count unchanged.</summary>
        Divide
    }
}

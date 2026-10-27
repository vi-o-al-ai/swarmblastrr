using System;
using Swarmblastrr.Track;

namespace Swarmblastrr.Level
{
    /// <summary>
    /// One entry of a <see cref="LevelDefinition"/>: what to place and where along +Z. Only the
    /// fields relevant to <see cref="Type"/> are meaningful; the others stay at their defaults.
    /// </summary>
    [Serializable]
    public struct LevelElementData
    {
        /// <summary>What kind of element this is.</summary>
        public LevelElementType Type;

        /// <summary>World-space Z of the element (the lane runs along +Z).</summary>
        public float Z;

        /// <summary>Operation of the left gate (<see cref="LevelElementType.GatePair"/>).</summary>
        public GateOperation LeftOp;

        /// <summary>Operand of the left gate (<see cref="LevelElementType.GatePair"/>).</summary>
        public int LeftValue;

        /// <summary>Operation of the right gate (<see cref="LevelElementType.GatePair"/>).</summary>
        public GateOperation RightOp;

        /// <summary>Operand of the right gate (<see cref="LevelElementType.GatePair"/>).</summary>
        public int RightValue;

        /// <summary>Number of enemy units (<see cref="LevelElementType.EnemyCrowd"/>).</summary>
        public int Count;

        /// <summary>Boss hit points (<see cref="LevelElementType.Boss"/>).</summary>
        public int BossHp;

        /// <summary>Number of multiplier tiles (<see cref="LevelElementType.RewardRamp"/>).</summary>
        public int RampTiles;
    }
}

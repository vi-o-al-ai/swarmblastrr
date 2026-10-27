using System.Collections.Generic;
using UnityEngine;

namespace Swarmblastrr.Level
{
    /// <summary>
    /// Data for one level: the floor length and the list of things placed along the lane, sorted
    /// by ascending Z. Normally produced at runtime by <see cref="LevelGenerator"/>, but it is a
    /// ScriptableObject so hand-authored levels can be saved as assets.
    /// </summary>
    [CreateAssetMenu(menuName = "Swarmblastrr/Level")]
    public class LevelDefinition : ScriptableObject
    {
        /// <summary>Units the crowd starts with when no GameConfig/upgrades apply (tests).</summary>
        public int StartingUnits = 10;

        /// <summary>Floor length along +Z. Must be at least the last element's Z plus 30.</summary>
        public float Length = 200f;

        /// <summary>Elements sorted by ascending <see cref="LevelElementData.Z"/>.</summary>
        public List<LevelElementData> Elements = new List<LevelElementData>();

        /// <summary>Creates a runtime instance with no elements and the default field values.</summary>
        public static LevelDefinition CreateEmpty()
        {
            LevelDefinition level = CreateInstance<LevelDefinition>();
            level.name = "Level";
            return level;
        }
    }
}

using UnityEngine;

namespace Swarmblastrr.Core
{
    /// <summary>
    /// Every gameplay tunable in one place. A default instance is created from code by
    /// <see cref="CreateDefault"/> when no asset is assigned to <see cref="GameBootstrap.ConfigOverride"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Swarmblastrr/Game Config")]
    public class GameConfig : ScriptableObject
    {
        /// <summary>Crowd speed along +Z, in world units per second.</summary>
        [Header("Movement")]
        public float ForwardSpeed = 8f;

        /// <summary>How fast the crowd centre follows the finger, in world units per second.</summary>
        public float LateralSpeed = 10f;

        /// <summary>Half the lane width; the crowd centre is clamped to [-LaneHalfWidth, +LaneHalfWidth].</summary>
        public float LaneHalfWidth = 3f;

        /// <summary>Visual and separation radius of a single unit.</summary>
        [Header("Units")]
        public float UnitRadius = 0.22f;

        /// <summary>Uniform mesh scale applied to one rendered unit.</summary>
        public float UnitScale = 0.45f;

        /// <summary>Hard cap on individually simulated and rendered units per crowd.</summary>
        public int MaxSimulatedUnits = 3000;

        /// <summary>Strength of the push that keeps neighbouring units apart.</summary>
        [Header("Flocking")]
        public float SeparationStrength = 25f;

        /// <summary>Strength of the pull toward the crowd centre.</summary>
        public float CohesionStrength = 6f;

        /// <summary>Velocity damping applied every step.</summary>
        public float Damping = 8f;

        /// <summary>Units per second exchanged in a crowd-vs-crowd fight.</summary>
        [Header("Combat")]
        public float EnemyKillRate = 40f;

        /// <summary>Player units per second consumed by the boss while fighting it.</summary>
        public float BossDrainRate = 60f;

        /// <summary>Coins awarded per surviving unit (before the ramp multiplier).</summary>
        [Header("Economy")]
        public int CoinsPerSurvivingUnit = 1;

        /// <summary>Flat bonus for clearing a level.</summary>
        public int LevelClearBonus = 50;

        /// <summary>Tint of the player's crowd.</summary>
        [Header("Visuals")]
        public Color PlayerColor = new Color(0.35f, 0.55f, 1f);

        /// <summary>Tint of enemy crowds.</summary>
        public Color EnemyColor = new Color(1f, 0.3f, 0.25f);

        /// <summary>Tint of the boss.</summary>
        public Color BossColor = new Color(0.85f, 0.7f, 0.45f);

        /// <summary>Optional material template for crowd rendering; null uses the built-in instanced shader.</summary>
        public Material CrowdMaterialOverride;

        /// <summary>Optional unit mesh; null uses the built-in capsule.</summary>
        public Mesh UnitMeshOverride;

        /// <summary>
        /// Creates a runtime instance carrying the default values declared on the fields above.
        /// </summary>
        public static GameConfig CreateDefault()
        {
            GameConfig config = CreateInstance<GameConfig>();
            config.name = "GameConfig (Default)";
            return config;
        }
    }
}

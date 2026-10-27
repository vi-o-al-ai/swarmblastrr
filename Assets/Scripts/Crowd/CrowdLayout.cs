using Unity.Mathematics;

namespace Swarmblastrr.Crowd
{
    /// <summary>
    /// Deterministic crowd layout helpers: sunflower (Vogel spiral) spawn offsets and the
    /// approximate footprint radius of a packed crowd. Pure math with no Unity engine
    /// dependencies, so it is safe to call from edit-mode tests.
    /// </summary>
    public static class CrowdLayout
    {
        /// <summary>Golden angle in radians, pi * (3 - sqrt(5)).</summary>
        private const float GoldenAngle = 2.3999632f;

        /// <summary>
        /// Radial scale of the spiral relative to the spacing. A Vogel spiral with
        /// r = c * sqrt(i) covers pi * c^2 of area per point; c = 0.525 * spacing gives a mean
        /// nearest-neighbour distance of roughly one spacing.
        /// </summary>
        private const float SpiralScale = 0.525f;

        /// <summary>Radius-per-sqrt(n) factor, a little above the spiral scale so the outer ring is covered.</summary>
        private const float RadiusScale = 0.6f;

        /// <summary>Deterministic spawn offset for unit <paramref name="index"/> (sunflower / Vogel spiral), spacing in world units.</summary>
        public static float2 SpawnOffset(int index, float spacing)
        {
            if (index < 0)
            {
                index = 0;
            }

            float radius = spacing * SpiralScale * math.sqrt(index + 0.5f);
            float angle = index * GoldenAngle;
            return new float2(radius * math.cos(angle), radius * math.sin(angle));
        }

        /// <summary>Approximate radius of a crowd of <paramref name="count"/> units packed with the given spacing. Grows with sqrt(count).</summary>
        public static float Radius(int count, float spacing)
        {
            if (count <= 0)
            {
                return 0f;
            }

            return spacing * RadiusScale * math.sqrt((float)count);
        }
    }
}

using NUnit.Framework;
using Unity.Mathematics;
using Swarmblastrr.Crowd;

namespace Swarmblastrr.Tests
{
    /// <summary>
    /// Edit-mode tests for <see cref="CrowdLayout"/>: spawn offsets are deterministic, pairwise
    /// distinct and lie inside the reported footprint, and the radius grows with the square root
    /// of the unit count.
    /// </summary>
    [TestFixture]
    public class CrowdLayoutTests
    {
        private const float Spacing = 0.22f * 2.2f;
        private const int SampleCount = 1000;
        private const float Tolerance = 1e-4f;

        /// <summary>No two spawn points may be closer than this many spacings.</summary>
        private const float MinSpawnSeparationFactor = 0.5f;

        [Test]
        public void SpawnOffset_IsDeterministic()
        {
            for (int i = 0; i < SampleCount; i++)
            {
                float2 a = CrowdLayout.SpawnOffset(i, Spacing);
                float2 b = CrowdLayout.SpawnOffset(i, Spacing);
                Assert.AreEqual(a.x, b.x, "unit " + i + " x");
                Assert.AreEqual(a.y, b.y, "unit " + i + " y");
            }
        }

        [Test]
        public void SpawnOffset_AreFinite()
        {
            for (int i = 0; i < SampleCount; i++)
            {
                float2 p = CrowdLayout.SpawnOffset(i, Spacing);
                Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(p.y), "unit " + i + " is NaN");
                Assert.IsFalse(float.IsInfinity(p.x) || float.IsInfinity(p.y), "unit " + i + " is infinite");
            }
        }

        [Test]
        public void SpawnOffset_AreDistinct()
        {
            float2[] offsets = Offsets(SampleCount, Spacing);
            float minDistance = MinPairwiseDistance(offsets);
            Assert.Greater(minDistance, 0f, "two spawn offsets coincide");
            Assert.GreaterOrEqual(minDistance, Spacing * MinSpawnSeparationFactor, "spawn offsets are packed too tightly");
        }

        [Test]
        public void SpawnOffset_FirstUnit_IsNearOrigin()
        {
            Assert.Less(math.length(CrowdLayout.SpawnOffset(0, Spacing)), Spacing);
        }

        [Test]
        public void SpawnOffset_DistanceFromCentreGrowsWithIndex()
        {
            float previous = -1f;
            for (int i = 0; i < SampleCount; i++)
            {
                float length = math.length(CrowdLayout.SpawnOffset(i, Spacing));
                Assert.Greater(length, previous, "unit " + i + " is not further out than unit " + (i - 1));
                previous = length;
            }
        }

        [Test]
        public void SpawnOffset_ScalesLinearlyWithSpacing()
        {
            for (int i = 0; i < SampleCount; i += 7)
            {
                float2 a = CrowdLayout.SpawnOffset(i, Spacing);
                float2 b = CrowdLayout.SpawnOffset(i, Spacing * 2f);
                Assert.AreEqual(a.x * 2f, b.x, Tolerance, "unit " + i + " x");
                Assert.AreEqual(a.y * 2f, b.y, Tolerance, "unit " + i + " y");
            }
        }

        [Test]
        public void SpawnOffset_NegativeIndex_IsTreatedAsZero()
        {
            float2 zero = CrowdLayout.SpawnOffset(0, Spacing);
            float2 negative = CrowdLayout.SpawnOffset(-1, Spacing);
            Assert.AreEqual(zero.x, negative.x);
            Assert.AreEqual(zero.y, negative.y);
        }

        [Test]
        public void SpawnOffset_AllUnitsLieWithinRadius()
        {
            int[] counts = { 1, 2, 10, 100, 500, SampleCount };
            foreach (int count in counts)
            {
                float radius = CrowdLayout.Radius(count, Spacing);
                for (int i = 0; i < count; i++)
                {
                    Assert.LessOrEqual(math.length(CrowdLayout.SpawnOffset(i, Spacing)), radius + Tolerance, "unit " + i + " of " + count);
                }
            }
        }

        [Test]
        public void Radius_ZeroOrNegativeCount_IsZero()
        {
            Assert.AreEqual(0f, CrowdLayout.Radius(0, Spacing));
            Assert.AreEqual(0f, CrowdLayout.Radius(-5, Spacing));
        }

        [Test]
        public void Radius_IsPositiveAndStrictlyIncreasing()
        {
            float previous = 0f;
            for (int n = 1; n <= SampleCount; n++)
            {
                float radius = CrowdLayout.Radius(n, Spacing);
                Assert.Greater(radius, previous, "count " + n);
                previous = radius;
            }
        }

        [Test]
        public void Radius_GrowsWithSquareRootOfCount()
        {
            int[] counts = { 1, 4, 25, 100, 250 };
            foreach (int n in counts)
            {
                Assert.AreEqual(2f * CrowdLayout.Radius(n, Spacing), CrowdLayout.Radius(4 * n, Spacing), 1e-3f, "4x count " + n);
                Assert.AreEqual(10f * CrowdLayout.Radius(n, Spacing), CrowdLayout.Radius(100 * n, Spacing), 1e-2f, "100x count " + n);
            }

            float ratio = CrowdLayout.Radius(1, Spacing);
            for (int n = 2; n <= SampleCount; n++)
            {
                Assert.AreEqual(ratio, CrowdLayout.Radius(n, Spacing) / math.sqrt((float)n), Tolerance, "count " + n);
            }
        }

        [Test]
        public void Radius_ScalesLinearlyWithSpacing()
        {
            Assert.AreEqual(3f * CrowdLayout.Radius(100, Spacing), CrowdLayout.Radius(100, 3f * Spacing), Tolerance);
            Assert.AreEqual(0f, CrowdLayout.Radius(100, 0f), Tolerance);
        }

        [Test]
        public void Radius_IsTightAroundTheSpiral()
        {
            int[] counts = { 1, 10, 100, SampleCount };
            foreach (int n in counts)
            {
                float outermost = math.length(CrowdLayout.SpawnOffset(n - 1, Spacing));
                float radius = CrowdLayout.Radius(n, Spacing);
                Assert.GreaterOrEqual(radius, outermost - Tolerance, "count " + n + " radius smaller than the outermost unit");
                Assert.LessOrEqual(radius, 2f * outermost + Spacing, "count " + n + " radius far larger than the crowd");
            }
        }

        private static float2[] Offsets(int count, float spacing)
        {
            float2[] offsets = new float2[count];
            for (int i = 0; i < count; i++)
            {
                offsets[i] = CrowdLayout.SpawnOffset(i, spacing);
            }

            return offsets;
        }

        private static float MinPairwiseDistance(float2[] points)
        {
            float minSq = float.MaxValue;
            for (int i = 0; i < points.Length; i++)
            {
                for (int j = i + 1; j < points.Length; j++)
                {
                    float distSq = math.distancesq(points[i], points[j]);
                    if (distSq < minSq)
                    {
                        minSq = distSq;
                    }
                }
            }

            return math.sqrt(minSq);
        }
    }
}

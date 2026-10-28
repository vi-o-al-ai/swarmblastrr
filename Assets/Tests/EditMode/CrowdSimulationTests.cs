using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Swarmblastrr.Crowd;

namespace Swarmblastrr.Tests
{
    /// <summary>
    /// Edit-mode tests for <see cref="CrowdSimulation"/>: 500 units stepped 120 times at 1/60 s stay
    /// finite, inside the lane and apart from each other; <see cref="CrowdSimulation.SetCount"/> grows
    /// and shrinks correctly; scheduling with no units and disposal are safe.
    /// </summary>
    [TestFixture]
    public class CrowdSimulationTests
    {
        private const int UnitCount = 500;
        private const int Steps = 120;
        private const float DeltaTime = 1f / 60f;
        private const float UnitRadius = 0.22f;
        private const float Spacing = UnitRadius * 2.2f;
        private const float LaneHalfWidth = 3f;
        private const float SeparationStrength = 25f;
        private const float CohesionStrength = 6f;
        private const float Damping = 8f;
        private const float LaneTolerance = 1e-3f;
        private const float MinSeparationFactor = 0.5f;
        private const float SpawnTolerance = 1e-5f;

        /// <summary>Generous bound on how far an unclamped crowd may drift from its centre.</summary>
        private const float CohesionBound = 50f;

        private CrowdSimulation _sim;

        [TearDown]
        public void TearDown()
        {
            if (_sim != null)
            {
                _sim.Dispose();
                _sim = null;
            }
        }

        [Test]
        public void Constructor_AllocatesCapacityWithNoUnits()
        {
            _sim = new CrowdSimulation(UnitCount, UnitRadius);
            Assert.IsTrue(_sim.IsCreated);
            Assert.AreEqual(UnitCount, _sim.Capacity);
            Assert.AreEqual(0, _sim.Count);
            Assert.AreEqual(UnitCount, _sim.Positions.Length);
        }

        [Test]
        public void FiveHundredUnits_120Steps_StayFiniteInsideLaneAndApart()
        {
            _sim = new CrowdSimulation(UnitCount, UnitRadius);
            _sim.SetCount(UnitCount);
            Assert.AreEqual(UnitCount, _sim.Count);

            Step(_sim, LaneParams(LaneHalfWidth), Steps);

            float2[] positions = Snapshot(_sim);
            Assert.AreEqual(UnitCount, positions.Length);
            AssertFinite(positions);
            AssertInsideLane(positions, LaneHalfWidth);

            float minDistance = MinPairwiseDistance(positions);
            Assert.GreaterOrEqual(minDistance, UnitRadius * MinSeparationFactor, "two units ended up closer than half a radius");
        }

        [Test]
        public void Lane_ClampsUnitsSpawnedOutsideIt_AfterOneStep()
        {
            _sim = new CrowdSimulation(UnitCount, UnitRadius);
            _sim.SetCount(UnitCount);
            Assert.Greater(_sim.Radius(Spacing), LaneHalfWidth, "the spawn footprint should be wider than the lane for this test to mean anything");

            Step(_sim, LaneParams(LaneHalfWidth), 1);

            float2[] positions = Snapshot(_sim);
            AssertFinite(positions);
            AssertInsideLane(positions, LaneHalfWidth);
        }

        [Test]
        public void NoLane_UnitsStayFiniteCohesiveAndApart()
        {
            _sim = new CrowdSimulation(UnitCount, UnitRadius);
            _sim.SetCount(UnitCount);

            Step(_sim, LaneParams(float.MaxValue), Steps);

            float2[] positions = Snapshot(_sim);
            AssertFinite(positions);
            for (int i = 0; i < positions.Length; i++)
            {
                Assert.Less(math.length(positions[i]), CohesionBound, "unit " + i + " drifted away from the crowd");
            }

            Assert.GreaterOrEqual(MinPairwiseDistance(positions), UnitRadius * MinSeparationFactor);
        }

        [Test]
        public void Step_ZeroDeltaTime_LeavesPositionsUnchanged()
        {
            _sim = new CrowdSimulation(100, UnitRadius);
            _sim.SetCount(50);
            float2[] before = Snapshot(_sim);

            CrowdSimParams frozen = LaneParams(LaneHalfWidth);
            frozen.DeltaTime = 0f;
            Step(_sim, frozen, 3);

            float2[] after = Snapshot(_sim);
            Assert.AreEqual(before.Length, after.Length);
            for (int i = 0; i < before.Length; i++)
            {
                Assert.AreEqual(before[i].x, after[i].x, "unit " + i + " x");
                Assert.AreEqual(before[i].y, after[i].y, "unit " + i + " y");
            }
        }

        [Test]
        public void SetCount_Grow_SpawnsAtLayoutOffsets()
        {
            _sim = new CrowdSimulation(100, UnitRadius);

            _sim.SetCount(50);
            Assert.AreEqual(50, _sim.Count);
            AssertSpawnedAtLayout(_sim, 0, 50);

            _sim.SetCount(80);
            Assert.AreEqual(80, _sim.Count);
            AssertSpawnedAtLayout(_sim, 50, 80);
            AssertSpawnedAtLayout(_sim, 0, 50);
        }

        [Test]
        public void SetCount_Shrink_DropsLastUnitsAndKeepsTheRest()
        {
            _sim = new CrowdSimulation(100, UnitRadius);
            _sim.SetCount(80);
            Step(_sim, LaneParams(LaneHalfWidth), 5);
            float2[] before = Snapshot(_sim);

            _sim.SetCount(30);
            Assert.AreEqual(30, _sim.Count);

            float2[] after = Snapshot(_sim);
            Assert.AreEqual(30, after.Length);
            for (int i = 0; i < after.Length; i++)
            {
                Assert.AreEqual(before[i].x, after[i].x, "unit " + i + " x");
                Assert.AreEqual(before[i].y, after[i].y, "unit " + i + " y");
            }
        }

        [Test]
        public void SetCount_ShrinkThenGrow_RespawnsDroppedUnits()
        {
            _sim = new CrowdSimulation(100, UnitRadius);
            _sim.SetCount(60);
            Step(_sim, LaneParams(LaneHalfWidth), 10);

            _sim.SetCount(20);
            _sim.SetCount(40);

            Assert.AreEqual(40, _sim.Count);
            AssertSpawnedAtLayout(_sim, 20, 40);
        }

        [Test]
        public void SetCount_ClampsToCapacityAndZero()
        {
            _sim = new CrowdSimulation(100, UnitRadius);

            _sim.SetCount(1000);
            Assert.AreEqual(100, _sim.Count);

            _sim.SetCount(-5);
            Assert.AreEqual(0, _sim.Count);

            _sim.SetCount(100);
            Assert.AreEqual(100, _sim.Count);
        }

        [Test]
        public void SetCount_SameCount_IsANoOp()
        {
            _sim = new CrowdSimulation(100, UnitRadius);
            _sim.SetCount(40);
            Step(_sim, LaneParams(LaneHalfWidth), 5);
            float2[] before = Snapshot(_sim);

            _sim.SetCount(40);

            float2[] after = Snapshot(_sim);
            Assert.AreEqual(40, _sim.Count);
            for (int i = 0; i < before.Length; i++)
            {
                Assert.AreEqual(before[i].x, after[i].x, "unit " + i + " x");
                Assert.AreEqual(before[i].y, after[i].y, "unit " + i + " y");
            }
        }

        [Test]
        public void SetCount_GrowWhileRunning_KeepsSimulationStable()
        {
            _sim = new CrowdSimulation(UnitCount, UnitRadius);
            _sim.SetCount(100);
            Step(_sim, LaneParams(LaneHalfWidth), 30);

            _sim.SetCount(UnitCount);
            Assert.AreEqual(UnitCount, _sim.Count);
            Step(_sim, LaneParams(LaneHalfWidth), 30);

            float2[] positions = Snapshot(_sim);
            Assert.AreEqual(UnitCount, positions.Length);
            AssertFinite(positions);
            AssertInsideLane(positions, LaneHalfWidth);
        }

        [Test]
        public void Radius_MatchesCrowdLayout()
        {
            _sim = new CrowdSimulation(100, UnitRadius);
            Assert.AreEqual(0f, _sim.Radius(Spacing), 1e-6f);

            _sim.SetCount(64);
            Assert.AreEqual(CrowdLayout.Radius(64, Spacing), _sim.Radius(Spacing), 1e-6f);
            Assert.AreEqual(CrowdLayout.Radius(64, 1f), _sim.Radius(1f), 1e-6f);
        }

        [Test]
        public void ZeroUnits_ScheduleAndComplete_DoNotThrow()
        {
            _sim = new CrowdSimulation(10, UnitRadius);
            Assert.AreEqual(0, _sim.Count);

            JobHandle handle = default;
            Assert.DoesNotThrow(() => handle = _sim.Schedule(LaneParams(LaneHalfWidth)));
            Assert.IsTrue(handle.IsCompleted, "with no units nothing should be scheduled");
            Assert.DoesNotThrow(() => _sim.Complete());

            Assert.AreEqual(0, _sim.Count);
            Assert.AreEqual(0f, _sim.Radius(Spacing));
            Assert.AreEqual(10, _sim.Positions.Length);
        }

        [Test]
        public void ZeroCapacity_DoesNotThrow()
        {
            _sim = new CrowdSimulation(0, UnitRadius);
            Assert.AreEqual(0, _sim.Capacity);

            Assert.DoesNotThrow(() => _sim.SetCount(5));
            Assert.AreEqual(0, _sim.Count);
            Assert.DoesNotThrow(() =>
            {
                _sim.Schedule(LaneParams(LaneHalfWidth));
                _sim.Complete();
            });
            Assert.AreEqual(0f, _sim.Radius(Spacing));
        }

        [Test]
        public void Complete_WithoutSchedule_IsSafe()
        {
            _sim = new CrowdSimulation(10, UnitRadius);
            Assert.DoesNotThrow(() => _sim.Complete());

            _sim.SetCount(5);
            Assert.DoesNotThrow(() => _sim.Complete());
            Assert.DoesNotThrow(() => _sim.Complete());
            Assert.AreEqual(5, _sim.Count);
        }

        [Test]
        public void Schedule_ReturnedHandle_CanBeCompletedByCaller()
        {
            _sim = new CrowdSimulation(100, UnitRadius);
            _sim.SetCount(100);

            JobHandle handle = _sim.Schedule(LaneParams(LaneHalfWidth));
            handle.Complete();
            Assert.IsTrue(handle.IsCompleted);
            _sim.Complete();

            float2[] positions = Snapshot(_sim);
            Assert.AreEqual(100, positions.Length);
            AssertFinite(positions);
        }

        [Test]
        public void Schedule_Twice_WithoutComplete_IsSafe()
        {
            _sim = new CrowdSimulation(100, UnitRadius);
            _sim.SetCount(100);

            Assert.DoesNotThrow(() =>
            {
                _sim.Schedule(LaneParams(LaneHalfWidth));
                _sim.Schedule(LaneParams(LaneHalfWidth));
                _sim.Complete();
            });

            AssertFinite(Snapshot(_sim));
        }

        [Test]
        public void Dispose_ReleasesBuffers_AndIsIdempotent()
        {
            CrowdSimulation sim = new CrowdSimulation(50, UnitRadius);
            try
            {
                sim.SetCount(50);
                Step(sim, LaneParams(LaneHalfWidth), 3);
                Assert.IsTrue(sim.IsCreated);
            }
            finally
            {
                sim.Dispose();
            }

            Assert.IsFalse(sim.IsCreated);
            Assert.AreEqual(0, sim.Count);
            Assert.DoesNotThrow(() => sim.Dispose());

            Assert.DoesNotThrow(() => sim.SetCount(10));
            Assert.AreEqual(0, sim.Count, "a disposed simulation cannot hold units");
            Assert.DoesNotThrow(() =>
            {
                sim.Schedule(LaneParams(LaneHalfWidth));
                sim.Complete();
            });
        }

        [Test]
        public void Dispose_WithPendingStep_CompletesItFirst()
        {
            CrowdSimulation sim = new CrowdSimulation(50, UnitRadius);
            bool disposed = false;
            try
            {
                sim.SetCount(50);
                sim.Schedule(LaneParams(LaneHalfWidth));
                Assert.DoesNotThrow(() => sim.Dispose());
                disposed = true;
                Assert.IsFalse(sim.IsCreated);
            }
            finally
            {
                if (!disposed)
                {
                    sim.Dispose();
                }
            }
        }

        private static CrowdSimParams LaneParams(float laneHalfWidth)
        {
            return new CrowdSimParams
            {
                DeltaTime = DeltaTime,
                UnitRadius = UnitRadius,
                LaneHalfWidth = laneHalfWidth,
                LaneCenterX = 0f,
                SeparationStrength = SeparationStrength,
                CohesionStrength = CohesionStrength,
                Damping = Damping,
                LocalCenterOffset = float2.zero,
                CellSize = UnitRadius * 2f,
            };
        }

        private static void Step(CrowdSimulation sim, CrowdSimParams p, int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                sim.Schedule(p);
                sim.Complete();
            }
        }

        private static float2[] Snapshot(CrowdSimulation sim)
        {
            NativeArray<float2> positions = sim.Positions;
            float2[] copy = new float2[sim.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = positions[i];
            }

            return copy;
        }

        private static void AssertFinite(float2[] positions)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                float2 p = positions[i];
                Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(p.y), "unit " + i + " is NaN");
                Assert.IsFalse(float.IsInfinity(p.x) || float.IsInfinity(p.y), "unit " + i + " is infinite");
            }
        }

        private static void AssertInsideLane(float2[] positions, float laneHalfWidth)
        {
            float limit = laneHalfWidth - UnitRadius + LaneTolerance;
            for (int i = 0; i < positions.Length; i++)
            {
                Assert.LessOrEqual(math.abs(positions[i].x), limit, "unit " + i + " left the lane");
            }
        }

        private static void AssertSpawnedAtLayout(CrowdSimulation sim, int from, int to)
        {
            NativeArray<float2> positions = sim.Positions;
            for (int i = from; i < to; i++)
            {
                float2 expected = CrowdLayout.SpawnOffset(i, Spacing);
                Assert.AreEqual(expected.x, positions[i].x, SpawnTolerance, "unit " + i + " x");
                Assert.AreEqual(expected.y, positions[i].y, SpawnTolerance, "unit " + i + " y");
            }
        }

        private static float MinPairwiseDistance(float2[] positions)
        {
            if (positions.Length < 2)
            {
                return float.MaxValue;
            }

            float minSq = float.MaxValue;
            for (int i = 0; i < positions.Length; i++)
            {
                for (int j = i + 1; j < positions.Length; j++)
                {
                    float distSq = math.distancesq(positions[i], positions[j]);
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

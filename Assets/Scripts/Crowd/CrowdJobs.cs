using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Swarmblastrr.Crowd
{
    /// <summary>
    /// Single-threaded job that rebuilds the spatial hash (cell hash to unit index) from the
    /// current positions. Runs before <see cref="FlockJob"/> every step.
    /// </summary>
    [BurstCompile]
    public struct BuildHashJob : IJob
    {
        /// <summary>Positions being hashed; only the first <see cref="Count"/> entries are used.</summary>
        [ReadOnly] public NativeArray<float2> Positions;

        /// <summary>Number of live units.</summary>
        public int Count;

        /// <summary>Spatial hash cell size.</summary>
        public float CellSize;

        /// <summary>Output map, cleared and refilled by this job.</summary>
        public NativeParallelMultiHashMap<int, int> Hash;

        /// <summary>Clears the map and inserts every live unit under the hash of its cell.</summary>
        public void Execute()
        {
            Hash.Clear();
            int count = math.min(Count, Positions.Length);
            for (int i = 0; i < count; i++)
            {
                Hash.Add(CrowdJobs.Hash(CrowdJobs.Cell(Positions[i], CellSize)), i);
            }
        }
    }

    /// <summary>
    /// Parallel flocking step. Per unit: separation from neighbours found in the 3x3 cells around
    /// it, cohesion toward <see cref="CrowdSimParams.LocalCenterOffset"/>, velocity damping,
    /// integration and the lane clamp. Reads <see cref="PositionsIn"/> and writes
    /// <see cref="PositionsOut"/> and <see cref="Velocities"/> at index i only, so there is no
    /// aliasing between threads.
    /// </summary>
    [BurstCompile]
    public struct FlockJob : IJobParallelFor
    {
        /// <summary>Repulsion weight cap for deeply overlapping units.</summary>
        private const float MaxPushWeight = 4f;

        /// <summary>Lane half-widths at or above this are treated as "no lane" (enemies pass float.MaxValue).</summary>
        private const float LaneClampThreshold = 1e6f;

        /// <summary>Positions at the start of the step.</summary>
        [ReadOnly] public NativeArray<float2> PositionsIn;

        /// <summary>Spatial hash built from <see cref="PositionsIn"/> by <see cref="BuildHashJob"/>.</summary>
        [ReadOnly] public NativeParallelMultiHashMap<int, int> Hash;

        /// <summary>Positions at the end of the step. Must be a different array from <see cref="PositionsIn"/>.</summary>
        [WriteOnly] public NativeArray<float2> PositionsOut;

        /// <summary>Velocities, read and written at index i only.</summary>
        public NativeArray<float2> Velocities;

        /// <summary>Step tunables.</summary>
        public CrowdSimParams Params;

        /// <summary>Steps unit <paramref name="i"/>.</summary>
        public void Execute(int i)
        {
            float2 p = PositionsIn[i];
            float2 v = Velocities[i];
            float dt = Params.DeltaTime;
            float interact = Params.UnitRadius * 2f;
            float interactSq = interact * interact;

            float2 separation = float2.zero;
            int2 cell = CrowdJobs.Cell(p, Params.CellSize);
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int key = CrowdJobs.Hash(new int2(cell.x + dx, cell.y + dy));
                    int j;
                    NativeParallelMultiHashMapIterator<int> it;
                    if (!Hash.TryGetFirstValue(key, out j, out it))
                    {
                        continue;
                    }

                    do
                    {
                        if (j == i)
                        {
                            continue;
                        }

                        float2 d = p - PositionsIn[j];
                        float distSq = math.lengthsq(d);
                        if (distSq >= interactSq)
                        {
                            continue;
                        }

                        separation += Push(d, distSq, interact, i, j);
                    }
                    while (Hash.TryGetNextValue(out j, ref it));
                }
            }

            // Spring toward the centre inside one unit of distance, constant-magnitude pull beyond it,
            // so the inward pressure on a large crowd's core stays bounded instead of growing with radius.
            float2 toCentre = Params.LocalCenterOffset - p;
            float2 cohesion = math.normalizesafe(toCentre) * (Params.CohesionStrength * math.min(1f, math.length(toCentre)));
            v += (separation * Params.SeparationStrength + cohesion) * dt;
            v *= math.max(0f, 1f - Params.Damping * dt);

            if (dt > 0f)
            {
                // Never move more than one interaction diameter per step so units cannot tunnel through each other.
                float2 delta = v * dt;
                float deltaSq = math.lengthsq(delta);
                float maxStep = math.max(interact, 1e-2f);
                if (deltaSq > maxStep * maxStep)
                {
                    delta *= maxStep / math.sqrt(deltaSq);
                    v = delta / dt;
                }

                p += delta;
            }

            if (Params.LaneHalfWidth < LaneClampThreshold)
            {
                float limit = math.max(0f, Params.LaneHalfWidth - Params.UnitRadius);
                float clampedX = math.clamp(p.x, Params.LaneCenterX - limit, Params.LaneCenterX + limit);
                if (clampedX != p.x)
                {
                    p.x = clampedX;
                    v.x = 0f;
                }
            }

            PositionsOut[i] = p;
            Velocities[i] = v;
        }

        /// <summary>
        /// Repulsion from a neighbour at offset <paramref name="d"/> = p_i - p_j. The weight is
        /// (2r / dist - 1), zero at the interaction distance and capped for deep overlaps.
        /// Coincident units are nudged apart along a deterministic axis chosen by index parity,
        /// antisymmetric in (i, j) so the pair really separates.
        /// </summary>
        private static float2 Push(float2 d, float distSq, float interact, int i, int j)
        {
            float2 dir;
            float dist;
            if (distSq < 1e-8f)
            {
                float2 axis = ((i + j) & 1) == 0 ? new float2(1f, 0f) : new float2(0f, 1f);
                dir = i < j ? axis : -axis;
                dist = 1e-4f;
            }
            else
            {
                dist = math.sqrt(distSq);
                dir = math.normalizesafe(d / dist);
            }

            float weight = math.min(interact / dist - 1f, MaxPushWeight);
            return dir * weight;
        }
    }

    /// <summary>Spatial hash helpers shared by the jobs and the simulation.</summary>
    public static class CrowdJobs
    {
        /// <summary>Integer cell containing <paramref name="p"/> for the given cell size.</summary>
        public static int2 Cell(float2 p, float cellSize)
        {
            return (int2)math.floor(p / cellSize);
        }

        /// <summary>Hash of a cell coordinate, used as the multi-hash-map key.</summary>
        public static int Hash(int2 cell)
        {
            return unchecked((cell.x * 73856093) ^ (cell.y * 19349663));
        }
    }
}

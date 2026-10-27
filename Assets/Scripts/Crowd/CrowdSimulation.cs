using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Swarmblastrr.Crowd
{
    /// <summary>
    /// Owns the native buffers for one crowd (player or enemy) and drives the Burst jobs that
    /// step it. Positions are double-buffered: <see cref="Schedule"/> reads the current buffer
    /// and writes the other, and <see cref="Complete"/> swaps them so <see cref="Positions"/>
    /// always exposes the latest completed step. Not a MonoBehaviour; the owner must call
    /// <see cref="Dispose"/> (normally from OnDestroy).
    /// </summary>
    public sealed class CrowdSimulation : IDisposable
    {
        /// <summary>IJobParallelFor batch size for the flock job.</summary>
        private const int FlockBatchSize = 64;

        /// <summary>Largest step the integrator accepts; longer frames are clamped for stability.</summary>
        private const float MaxDeltaTime = 0.05f;

        private readonly float _unitRadius;
        private readonly float _spacing;

        private NativeArray<float2> _positionsA;
        private NativeArray<float2> _positionsB;
        private NativeArray<float2> _velocities;
        private NativeParallelMultiHashMap<int, int> _hash;
        private JobHandle _handle;
        private int _count;
        private bool _aIsCurrent = true;
        private bool _stepPending;

        /// <summary>Allocated size; <see cref="Count"/> never exceeds it.</summary>
        public int Capacity { get; }

        /// <summary>Number of units currently simulated.</summary>
        public int Count => _count;

        /// <summary>Latest completed positions as local (x, z) offsets. Valid only after <see cref="Complete"/>.</summary>
        public NativeArray<float2> Positions => _aIsCurrent ? _positionsA : _positionsB;

        /// <summary>True while the native buffers are allocated.</summary>
        public bool IsCreated => _positionsA.IsCreated;

        /// <summary>Allocates persistent buffers for <paramref name="capacity"/> units. Spawn spacing is unitRadius * 2.2.</summary>
        public CrowdSimulation(int capacity, float unitRadius)
        {
            Capacity = math.max(0, capacity);
            _unitRadius = unitRadius;
            _spacing = unitRadius * 2.2f;
            _positionsA = new NativeArray<float2>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _positionsB = new NativeArray<float2>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _velocities = new NativeArray<float2>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _hash = new NativeParallelMultiHashMap<int, int>(math.max(1, Capacity), Allocator.Persistent);
        }

        /// <summary>
        /// Sets the live unit count, clamped to <see cref="Capacity"/>. Growing spawns the new units at
        /// <see cref="CrowdLayout.SpawnOffset"/> with zero velocity; shrinking drops the last units.
        /// Completes any pending step first.
        /// </summary>
        public void SetCount(int count)
        {
            Complete();
            if (!IsCreated)
            {
                _count = 0;
                return;
            }

            count = math.clamp(count, 0, Capacity);
            if (count > _count)
            {
                NativeArray<float2> current = Positions;
                NativeArray<float2> other = _aIsCurrent ? _positionsB : _positionsA;
                for (int i = _count; i < count; i++)
                {
                    float2 offset = CrowdLayout.SpawnOffset(i, _spacing);
                    current[i] = offset;
                    other[i] = offset;
                    _velocities[i] = float2.zero;
                }
            }

            _count = count;
        }

        /// <summary>
        /// Schedules one step: <see cref="BuildHashJob"/> then <see cref="FlockJob"/>. Completes any
        /// previous step first. Returns a default handle (and schedules nothing) when there are no units.
        /// Buffers are swapped by <see cref="Complete"/>, not here.
        /// </summary>
        public JobHandle Schedule(CrowdSimParams p, JobHandle dependsOn = default)
        {
            Complete();
            if (_count == 0 || !IsCreated)
            {
                return default;
            }

            if (p.CellSize <= 1e-5f)
            {
                p.CellSize = math.max(_unitRadius * 2f, 1e-3f);
            }

            p.DeltaTime = math.clamp(p.DeltaTime, 0f, MaxDeltaTime);

            if (_hash.Capacity < _count)
            {
                _hash.Capacity = _count;
            }

            NativeArray<float2> input = _aIsCurrent ? _positionsA : _positionsB;
            NativeArray<float2> output = _aIsCurrent ? _positionsB : _positionsA;

            BuildHashJob buildHash = new BuildHashJob
            {
                Positions = input,
                Count = _count,
                CellSize = p.CellSize,
                Hash = _hash,
            };
            JobHandle hashHandle = buildHash.Schedule(dependsOn);

            FlockJob flock = new FlockJob
            {
                PositionsIn = input,
                Hash = _hash,
                PositionsOut = output,
                Velocities = _velocities,
                Params = p,
            };
            _handle = flock.Schedule(_count, FlockBatchSize, hashHandle);
            _stepPending = true;
            return _handle;
        }

        /// <summary>
        /// Completes the last scheduled step and swaps the position buffers. Must be called before
        /// reading <see cref="Positions"/> or calling <see cref="SetCount"/> / <see cref="Schedule"/>
        /// again. Safe to call when nothing is pending.
        /// </summary>
        public void Complete()
        {
            if (!_stepPending)
            {
                return;
            }

            _handle.Complete();
            _handle = default;
            _stepPending = false;
            _aIsCurrent = !_aIsCurrent;
        }

        /// <summary>Footprint radius of the current crowd, <see cref="CrowdLayout.Radius"/> of <see cref="Count"/>.</summary>
        public float Radius(float spacing)
        {
            return CrowdLayout.Radius(_count, spacing);
        }

        /// <summary>Completes any pending step and frees the native buffers. Idempotent.</summary>
        public void Dispose()
        {
            Complete();
            if (_positionsA.IsCreated)
            {
                _positionsA.Dispose();
            }

            if (_positionsB.IsCreated)
            {
                _positionsB.Dispose();
            }

            if (_velocities.IsCreated)
            {
                _velocities.Dispose();
            }

            if (_hash.IsCreated)
            {
                _hash.Dispose();
            }

            _count = 0;
        }
    }
}

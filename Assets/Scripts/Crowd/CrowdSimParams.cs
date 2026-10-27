using Unity.Mathematics;

namespace Swarmblastrr.Crowd
{
    /// <summary>
    /// Per-step tunables handed to <see cref="CrowdSimulation.Schedule"/>. A plain blittable
    /// struct so the Burst jobs can carry it by value.
    /// </summary>
    public struct CrowdSimParams
    {
        /// <summary>Simulation step in seconds.</summary>
        public float DeltaTime;

        /// <summary>Visual and separation radius of one unit.</summary>
        public float UnitRadius;

        /// <summary>Local clamp: |x| &lt;= LaneHalfWidth - UnitRadius. Enemies pass float.MaxValue to disable the clamp.</summary>
        public float LaneHalfWidth;

        /// <summary>
        /// Lane centre in local x, generalising the clamp to |x - LaneCenterX| &lt;= LaneHalfWidth - UnitRadius.
        /// Normally 0. The player crowd passes the negated centre x so both lane walls stay fixed in
        /// world space while its local frame moves with the centre.
        /// </summary>
        public float LaneCenterX;

        /// <summary>Acceleration scale of the neighbour repulsion.</summary>
        public float SeparationStrength;

        /// <summary>Spring strength pulling every unit toward <see cref="LocalCenterOffset"/>.</summary>
        public float CohesionStrength;

        /// <summary>Velocity damping per second.</summary>
        public float Damping;

        /// <summary>Where the crowd should gather, in local space (normally zero).</summary>
        public float2 LocalCenterOffset;

        /// <summary>Spatial hash cell size, = UnitRadius * 2.</summary>
        public float CellSize;
    }
}

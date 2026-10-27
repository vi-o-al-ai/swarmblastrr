using Unity.Mathematics;
using UnityEngine;
using Swarmblastrr.Controls;
using Swarmblastrr.Core;
using Swarmblastrr.Track;

namespace Swarmblastrr.Crowd
{
    /// <summary>
    /// The player's crowd. Holds the logical unit count (which may exceed the simulated cap),
    /// moves the crowd centre along the lane from swipe input, steps the simulation in Update and
    /// completes and renders it in LateUpdate. Raises <see cref="GameEvents.CountChanged"/> on
    /// every count change and calls <see cref="GameManager.Lose"/> when the crowd is wiped out mid-run.
    /// </summary>
    public sealed class CrowdController : MonoBehaviour
    {
        /// <summary>Smallest distance (in unit radii) the crowd centre keeps from the lane walls.</summary>
        private const float MinWallMarginRadii = 2f;

        private GameConfig _config;
        private SwipeInput _input;
        private CrowdRenderer _renderer;
        private CrowdSimulation _simulation;
        private Material _ownedMaterial;
        private int _count;
        private float _targetX;
        private bool _isRunning;

        /// <summary>Logical unit count; may exceed <c>GameConfig.MaxSimulatedUnits</c>.</summary>
        public int Count => _count;

        /// <summary>Moves forward along +Z while true.</summary>
        public bool IsRunning
        {
            get => _isRunning;
            set => _isRunning = value;
        }

        /// <summary>Desired centre x. The setter clamps it so the crowd stays inside the lane.</summary>
        public float TargetX
        {
            get => _targetX;
            set => _targetX = ClampCentreX(value);
        }

        /// <summary>World position of the crowd centre (ground level).</summary>
        public Vector3 Position => transform.position;

        /// <summary>Leading edge of the crowd along +Z.</summary>
        public float FrontZ => Position.z + Radius;

        /// <summary>Footprint radius of the simulated part of the crowd.</summary>
        public float Radius => CrowdLayout.Radius(Mathf.Min(_count, MaxSimulated), Spacing);

        /// <summary>The underlying simulation; null until <see cref="Configure"/> runs.</summary>
        public CrowdSimulation Simulation => _simulation;

        private int MaxSimulated
        {
            get
            {
                if (_simulation != null)
                {
                    return _simulation.Capacity;
                }

                return _config != null ? _config.MaxSimulatedUnits : 0;
            }
        }

        private float UnitRadius => _config != null ? _config.UnitRadius : 0.22f;

        private float Spacing => UnitRadius * 2.2f;

        /// <summary>Creates the simulation (capacity = config.MaxSimulatedUnits) and the renderer's material and mesh in PlayerColor.</summary>
        public void Configure(GameConfig config, SwipeInput input)
        {
            if (config == null)
            {
                config = GameConfig.CreateDefault();
            }

            _config = config;
            _input = input;

            if (_simulation != null)
            {
                _simulation.Dispose();
                _simulation = null;
            }

            int capacity = Mathf.Max(1, config.MaxSimulatedUnits);
            _simulation = new CrowdSimulation(capacity, config.UnitRadius);

            _renderer = GetComponent<CrowdRenderer>();
            if (_renderer == null)
            {
                _renderer = gameObject.AddComponent<CrowdRenderer>();
            }

            Mesh mesh = config.UnitMeshOverride != null ? config.UnitMeshOverride : CrowdMaterials.DefaultUnitMesh();
            ReleaseOwnedMaterial();
            Material material = CrowdMaterials.Create(config.PlayerColor, config.CrowdMaterialOverride);
            _ownedMaterial = material;
            _renderer.Configure(mesh, material, config.UnitScale);

            _simulation.SetCount(Mathf.Min(_count, capacity));
        }

        /// <summary>Teleports the crowd, re-lays the units out in a fresh spiral, sets the count, stops running and recentres.</summary>
        public void ResetTo(Vector3 position, int count)
        {
            transform.position = position;
            _isRunning = false;
            _targetX = 0f;
            if (_simulation != null)
            {
                _simulation.SetCount(0);
            }

            SetCount(count);
        }

        /// <summary>Clamps to [0, GateMath.MaxCount], raises GameEvents.CountChanged(prev, cur) and syncs the simulation to min(count, MaxSimulated).</summary>
        public void SetCount(int count)
        {
            int previous = _count;
            int current = Mathf.Clamp(count, 0, GateMath.MaxCount);
            _count = current;

            if (_simulation != null)
            {
                _simulation.SetCount(Mathf.Min(current, _simulation.Capacity));
            }

            GameEvents.RaiseCountChanged(previous, current);

            if (current == 0 && _isRunning)
            {
                _isRunning = false;
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.Lose();
                }
            }
        }

        /// <summary>Adds units (negative amounts remove). Returns the new count.</summary>
        public int AddUnits(int amount)
        {
            long next = (long)_count + amount;
            if (next < 0)
            {
                next = 0;
            }

            if (next > GateMath.MaxCount)
            {
                next = GateMath.MaxCount;
            }

            SetCount((int)next);
            return _count;
        }

        /// <summary>Removes up to <paramref name="amount"/> units. Returns the number actually removed.</summary>
        public int RemoveUnits(int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            int removed = Mathf.Min(amount, _count);
            SetCount(_count - removed);
            return removed;
        }

        /// <summary>Applies a gate: SetCount(GateMath.Apply(...)) then GameEvents.RaiseGateHit(op, before, after, worldPos).</summary>
        public void ApplyGate(GateOperation op, int value, Vector3 worldPos)
        {
            int before = _count;
            SetCount(GateMath.Apply(before, op, value));
            GameEvents.RaiseGateHit(op, before, _count, worldPos);
        }

        private void Update()
        {
            if (_simulation == null || _config == null)
            {
                return;
            }

            float dt = Time.deltaTime;

            if (_input != null)
            {
                float deltaX = _input.DeltaX;
                if (deltaX != 0f)
                {
                    TargetX = _targetX + deltaX;
                }
            }

            Vector3 pos = transform.position;
            float follow = 1f - Mathf.Exp(-_config.LateralSpeed * dt);
            pos.x = Mathf.Lerp(pos.x, _targetX, follow);
            pos.x = Mathf.Clamp(pos.x, -_config.LaneHalfWidth, _config.LaneHalfWidth);
            if (_isRunning)
            {
                pos.z += _config.ForwardSpeed * dt;
            }

            transform.position = pos;

            if (_simulation.Count == 0)
            {
                return;
            }

            // The simulation runs in the crowd's local frame, so the lane centre sits at local x = -pos.x.
            // Passing it with the full half-width keeps both walls fixed in world space (world |x| never
            // exceeds LaneHalfWidth - UnitRadius) without squeezing the far side when the centre steers.
            CrowdSimParams p = new CrowdSimParams
            {
                DeltaTime = dt,
                UnitRadius = _config.UnitRadius,
                LaneHalfWidth = _config.LaneHalfWidth,
                LaneCenterX = -pos.x,
                SeparationStrength = _config.SeparationStrength,
                CohesionStrength = _config.CohesionStrength,
                Damping = _config.Damping,
                LocalCenterOffset = float2.zero,
                CellSize = _config.UnitRadius * 2f,
            };
            _simulation.Schedule(p);
        }

        private void LateUpdate()
        {
            if (_simulation == null)
            {
                return;
            }

            _simulation.Complete();
            if (_renderer != null && _simulation.IsCreated)
            {
                _renderer.Render(_simulation.Positions, _simulation.Count, transform.position);
            }
        }

        private void OnDestroy()
        {
            if (_simulation != null)
            {
                _simulation.Dispose();
                _simulation = null;
            }

            ReleaseOwnedMaterial();
        }

        /// <summary>
        /// Destroys the material this controller created through <see cref="CrowdMaterials.Create"/>.
        /// A material assigned to the renderer from elsewhere is never destroyed.
        /// </summary>
        private void ReleaseOwnedMaterial()
        {
            Material owned = _ownedMaterial;
            _ownedMaterial = null;
            if (owned == null)
            {
                return;
            }

            if (_renderer != null && _renderer.Material == owned)
            {
                _renderer.Material = null;
            }

            if (Application.isPlaying)
            {
                Destroy(owned);
            }
            else
            {
                DestroyImmediate(owned);
            }
        }

        /// <summary>Keeps the centre far enough from the walls that the (capped) footprint fits, so the crowd is never squeezed to a line.</summary>
        private float ClampCentreX(float x)
        {
            if (_config == null)
            {
                return x;
            }

            float lane = _config.LaneHalfWidth;
            float margin = Mathf.Min(Mathf.Max(Radius, UnitRadius * MinWallMarginRadii), lane * 0.5f);
            float limit = Mathf.Max(0f, lane - margin);
            return Mathf.Clamp(x, -limit, limit);
        }
    }
}

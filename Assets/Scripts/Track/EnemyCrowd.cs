using Unity.Mathematics;
using UnityEngine;
using Swarmblastrr.Core;
using Swarmblastrr.Crowd;

namespace Swarmblastrr.Track
{
    /// <summary>
    /// A hostile crowd standing on the lane. It owns its own <see cref="CrowdSimulation"/> and a
    /// <see cref="CrowdRenderer"/> child so it mills in place, and trades units with the player
    /// at <c>GameConfig.EnemyKillRate</c> per second while the two crowd circles overlap. When its
    /// count reaches zero it is consumed and its visuals are destroyed.
    /// </summary>
    public class EnemyCrowd : TrackElement
    {
        private const float DefaultKillRate = 40f;
        private const float MarkerAlpha = 0.3f;
        private const float MarkerThickness = 0.02f;

        /// <summary>Remaining enemy units.</summary>
        public int Count { get; private set; }

        private GameConfig _config;
        private CrowdSimulation _simulation;
        private CrowdRenderer _renderer;
        private Material _material;
        private GameObject _visualRoot;
        private Transform _marker;
        private int _capacity;
        private float _spacing;
        private float _unitRadius;
        private bool _jobPending;

        /// <summary>
        /// Sets the unit count, allocates a simulation with capacity min(count, MaxSimulatedUnits)
        /// and builds the renderer child in <c>GameConfig.EnemyColor</c>.
        /// </summary>
        public void Configure(int count, GameConfig config)
        {
            _config = config != null ? config : GameConfig.CreateDefault();
            Count = Mathf.Clamp(count, 0, GateMath.MaxCount);
            HalfWidth = _config.LaneHalfWidth;
            _unitRadius = Mathf.Max(0.01f, _config.UnitRadius);
            _spacing = _unitRadius * 2.2f;

            DisposeSimulation();
            _capacity = Mathf.Max(1, Mathf.Min(Count, _config.MaxSimulatedUnits));
            _simulation = new CrowdSimulation(_capacity, _unitRadius);
            _simulation.SetCount(SimulatedCount);

            BuildVisuals(_config);
        }

        /// <summary>Circle-vs-circle test in XZ using the player's radius and this crowd's radius.</summary>
        public override bool Overlaps(CrowdController player)
        {
            if (Consumed || Count <= 0 || player == null)
            {
                return false;
            }

            Vector3 playerPos = player.Position;
            Vector3 ownPos = transform.position;
            float dx = playerPos.x - ownPos.x;
            float dz = playerPos.z - ownPos.z;
            float reach = player.Radius + CrowdRadius;
            return dx * dx + dz * dz <= reach * reach;
        }

        /// <summary>
        /// Exchanges k = clamp(round(EnemyKillRate * dt), 1, min(Count, player.Count)) units with the
        /// player. Shrinks the simulated crowd to match and consumes the element at zero.
        /// </summary>
        public override void OnCrowdOverlap(CrowdController player, float deltaTime)
        {
            if (Consumed || Count <= 0 || player == null)
            {
                return;
            }

            float killRate = _config != null ? _config.EnemyKillRate : DefaultKillRate;
            int k = Mathf.Max(1, Mathf.RoundToInt(killRate * deltaTime));
            k = Mathf.Min(k, Mathf.Min(Count, player.Count));
            if (k <= 0)
            {
                return;
            }

            player.RemoveUnits(k);
            Count -= k;

            if (Count <= 0)
            {
                Count = 0;
                Consumed = true;
                DestroyVisuals();
                DisposeSimulation();
                return;
            }

            SyncSimulationCount();
        }

        /// <summary>
        /// Creates the renderer child and a translucent footprint disc under the crowd. A no-op
        /// (apart from refreshing the footprint) when <see cref="Configure"/> has already built the
        /// visuals for the same config, so the builder's follow-up call does not tear them down.
        /// </summary>
        public override void BuildVisuals(GameConfig config)
        {
            if (_visualRoot != null && (config == null || config == _config))
            {
                UpdateMarker();
                return;
            }

            if (config != null)
            {
                _config = config;
            }

            DestroyVisuals();
            if (_config == null)
            {
                return;
            }

            _visualRoot = new GameObject("Visuals");
            _visualRoot.transform.SetParent(transform, false);

            GameObject rendererGo = new GameObject("CrowdRenderer");
            rendererGo.transform.SetParent(_visualRoot.transform, false);
            _renderer = rendererGo.AddComponent<CrowdRenderer>();

            Mesh mesh = _config.UnitMeshOverride != null ? _config.UnitMeshOverride : CrowdMaterials.DefaultUnitMesh();
            _material = CrowdMaterials.Create(_config.EnemyColor, _config.CrowdMaterialOverride);
            _renderer.Configure(mesh, _material, _config.UnitScale);

            Color markerColor = _config.EnemyColor * 0.6f;
            markerColor.a = MarkerAlpha;
            GameObject marker = TrackVisuals.CreatePrimitive(
                "Marker", _visualRoot.transform, TrackVisuals.CylinderMesh(), TrackVisuals.SolidMaterial(markerColor),
                new Vector3(0f, MarkerThickness * 1.5f, 0f), Vector3.one);
            _marker = marker.transform;
            UpdateMarker();
        }

        private int SimulatedCount => Mathf.Clamp(Count, 0, _capacity);

        private float CrowdRadius => CrowdLayout.Radius(SimulatedCount, _spacing);

        private void Update()
        {
            if (Consumed || _jobPending || _config == null || _simulation == null || !_simulation.IsCreated)
            {
                return;
            }

            if (_simulation.Count <= 0)
            {
                return;
            }

            CrowdSimParams p = new CrowdSimParams
            {
                DeltaTime = Time.deltaTime,
                UnitRadius = _unitRadius,
                LaneHalfWidth = float.MaxValue,
                SeparationStrength = _config.SeparationStrength,
                CohesionStrength = _config.CohesionStrength,
                Damping = _config.Damping,
                LocalCenterOffset = float2.zero,
                CellSize = _unitRadius * 2f
            };

            _simulation.Schedule(p);
            _jobPending = true;
        }

        private void LateUpdate()
        {
            CompletePending();

            if (Consumed || _renderer == null || _simulation == null || !_simulation.IsCreated)
            {
                return;
            }

            int count = _simulation.Count;
            if (count <= 0)
            {
                return;
            }

            _renderer.Render(_simulation.Positions, count, transform.position);
        }

        private void OnDestroy()
        {
            ReleaseMaterial();
            DisposeSimulation();
        }

        private void SyncSimulationCount()
        {
            if (_simulation != null && _simulation.IsCreated)
            {
                CompletePending();
                _simulation.SetCount(SimulatedCount);
            }

            UpdateMarker();
        }

        private void UpdateMarker()
        {
            if (_marker == null)
            {
                return;
            }

            float radius = CrowdRadius + _unitRadius;
            float diameter = Mathf.Max(0.1f, radius * 2f);
            _marker.localScale = new Vector3(diameter, MarkerThickness * 0.5f, diameter);
        }

        private void CompletePending()
        {
            if (!_jobPending)
            {
                return;
            }

            _jobPending = false;
            if (_simulation != null && _simulation.IsCreated)
            {
                _simulation.Complete();
            }
        }

        private void DisposeSimulation()
        {
            CompletePending();
            if (_simulation == null)
            {
                return;
            }

            if (_simulation.IsCreated)
            {
                _simulation.Dispose();
            }

            _simulation = null;
        }

        private void DestroyVisuals()
        {
            ReleaseMaterial();
            if (_visualRoot != null)
            {
                Destroy(_visualRoot);
            }

            _visualRoot = null;
            _renderer = null;
            _marker = null;
        }

        /// <summary>
        /// Destroys the runtime material created for the renderer. Destroying the visual hierarchy
        /// only removes GameObjects; the Material is an asset-like object that would otherwise leak
        /// once per build and per kill.
        /// </summary>
        private void ReleaseMaterial()
        {
            if (_material != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_material);
                }
                else
                {
                    DestroyImmediate(_material);
                }
            }

            _material = null;
        }
    }
}

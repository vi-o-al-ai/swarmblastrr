using System.Collections.Generic;
using UnityEngine;
using Swarmblastrr.Core;
using Swarmblastrr.Track;

namespace Swarmblastrr.Level
{
    /// <summary>
    /// Turns a <see cref="LevelDefinition"/> into scene objects: a floor, two rails and one
    /// <see cref="TrackElement"/> per element (a gate pair becomes two <see cref="Gate"/>s at
    /// x = -LaneHalfWidth/2 and +LaneHalfWidth/2). Everything lives under <see cref="Root"/>, which is
    /// destroyed and recreated on every <see cref="Build"/>. No prefabs: visuals are primitives.
    /// </summary>
    public class LevelBuilder : MonoBehaviour
    {
        private const string RootName = "Level";
        private const float LengthMargin = 30f;
        private const float RampTileLength = 2f;
        private const int RampUnitsPerTile = 20;
        private const int DefaultRampTiles = 10;

        private static readonly Color _floorColor = new Color(0.55f, 0.55f, 0.6f);
        private static readonly Color _railColor = new Color(0.4f, 0.3f, 0.2f);

        private readonly List<TrackElement> _elements = new List<TrackElement>();
        private GameConfig _fallbackConfig;

        /// <summary>Parent of everything built for the current level; null when nothing is built.</summary>
        public Transform Root { get; private set; }

        /// <summary>Built elements sorted by ascending Z. An entry compares equal to null once destroyed.</summary>
        public IReadOnlyList<TrackElement> Elements => _elements;

        /// <summary>The definition passed to the last <see cref="Build"/>; null after <see cref="Clear"/>.</summary>
        public LevelDefinition Current { get; private set; }

        /// <summary>
        /// Clears any previous level, then builds the floor, the rails and every element of
        /// <paramref name="level"/> under a new <see cref="Root"/>. A null <paramref name="config"/>
        /// falls back to <see cref="GameConfig.CreateDefault"/>.
        /// </summary>
        public void Build(LevelDefinition level, GameConfig config)
        {
            Clear();
            if (level == null)
            {
                return;
            }

            Current = level;
            GameConfig resolved = ResolveConfig(config);
            float laneHalfWidth = Mathf.Max(0.1f, resolved.LaneHalfWidth);

            GameObject rootGo = new GameObject(RootName);
            rootGo.transform.position = Vector3.zero;
            rootGo.transform.rotation = Quaternion.identity;
            Root = rootGo.transform;

            List<LevelElementData> source = level.Elements;
            List<int> order = SortedIndices(source);
            float floorLength = FloorLength(level, source, order);

            TrackVisuals.CreateFloor(Root, floorLength, laneHalfWidth, _floorColor);
            TrackVisuals.CreateRails(Root, floorLength, laneHalfWidth, _railColor);

            for (int i = 0; i < order.Count; i++)
            {
                BuildElement(source[order[i]], i, resolved, laneHalfWidth);
            }
        }

        /// <summary>Destroys <see cref="Root"/> and forgets the elements and <see cref="Current"/>.</summary>
        public void Clear()
        {
            _elements.Clear();
            Current = null;

            if (Root == null)
            {
                return;
            }

            GameObject rootGo = Root.gameObject;
            Root = null;

            if (Application.isPlaying)
            {
                Destroy(rootGo);
            }
            else
            {
                DestroyImmediate(rootGo);
            }
        }

        private void OnDestroy()
        {
            Clear();
        }

        private GameConfig ResolveConfig(GameConfig config)
        {
            if (config != null)
            {
                return config;
            }

            if (_fallbackConfig == null)
            {
                _fallbackConfig = GameConfig.CreateDefault();
            }

            return _fallbackConfig;
        }

        /// <summary>Indices into <paramref name="source"/> sorted by Z, ties keeping definition order.</summary>
        private static List<int> SortedIndices(List<LevelElementData> source)
        {
            List<int> order = new List<int>();
            if (source == null)
            {
                return order;
            }

            for (int i = 0; i < source.Count; i++)
            {
                order.Add(i);
            }

            order.Sort((a, b) =>
            {
                int byZ = source[a].Z.CompareTo(source[b].Z);
                return byZ != 0 ? byZ : a.CompareTo(b);
            });

            return order;
        }

        /// <summary>The definition's Length, extended if it would not cover the last element plus a margin.</summary>
        private static float FloorLength(LevelDefinition level, List<LevelElementData> source, List<int> order)
        {
            float minimumLength = 0f;
            for (int i = 0; i < order.Count; i++)
            {
                LevelElementData data = source[order[i]];
                float end = data.Z;
                if (data.Type == LevelElementType.RewardRamp)
                {
                    end += RampTiles(data) * RampTileLength;
                }

                minimumLength = Mathf.Max(minimumLength, end + LengthMargin);
            }

            return Mathf.Max(level.Length, minimumLength);
        }

        private static int RampTiles(LevelElementData data)
        {
            return data.RampTiles > 0 ? data.RampTiles : DefaultRampTiles;
        }

        private void BuildElement(LevelElementData data, int index, GameConfig config, float laneHalfWidth)
        {
            switch (data.Type)
            {
                case LevelElementType.GatePair:
                {
                    float halfWidth = laneHalfWidth * 0.5f;
                    BuildGate("Gate" + index + "L", -halfWidth, data.Z, data.LeftOp, data.LeftValue, halfWidth, config);
                    BuildGate("Gate" + index + "R", halfWidth, data.Z, data.RightOp, data.RightValue, halfWidth, config);
                    break;
                }

                case LevelElementType.EnemyCrowd:
                {
                    EnemyCrowd enemy = CreateElement<EnemyCrowd>("Enemy" + index, 0f, data.Z);
                    enemy.Configure(data.Count, config);
                    enemy.BuildVisuals(config);
                    _elements.Add(enemy);
                    break;
                }

                case LevelElementType.Boss:
                {
                    Boss boss = CreateElement<Boss>("Boss" + index, 0f, data.Z);
                    boss.Configure(data.BossHp, config);
                    boss.HalfWidth = laneHalfWidth;
                    boss.BuildVisuals(config);
                    _elements.Add(boss);
                    break;
                }

                case LevelElementType.RewardRamp:
                {
                    RewardRamp ramp = CreateElement<RewardRamp>("RewardRamp" + index, 0f, data.Z);
                    ramp.Configure(RampTiles(data), RampTileLength, RampUnitsPerTile, config);
                    ramp.HalfWidth = laneHalfWidth;
                    ramp.BuildVisuals(config);
                    _elements.Add(ramp);
                    break;
                }

                default:
                {
                    Debug.LogWarning("[Swarmblastrr] LevelBuilder: unknown element type " + data.Type + " at Z=" + data.Z);
                    break;
                }
            }
        }

        private void BuildGate(string name, float x, float z, GateOperation op, int value, float halfWidth, GameConfig config)
        {
            Gate gate = CreateElement<Gate>(name, x, z);
            gate.Configure(op, value, halfWidth);
            gate.BuildVisuals(config);
            _elements.Add(gate);
        }

        private T CreateElement<T>(string name, float x, float z) where T : TrackElement
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(Root, false);
            go.transform.localPosition = new Vector3(x, 0f, z);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.AddComponent<T>();
        }
    }
}

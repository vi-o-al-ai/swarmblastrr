using System;
using UnityEngine;
using Swarmblastrr.Core;
using Swarmblastrr.Crowd;

namespace Swarmblastrr.Track
{
    /// <summary>
    /// The row of multiplier tiles after the boss. The crowd keeps running; every
    /// <see cref="UnitsPerTile"/> surviving units let it reach one tile further, and when the crowd
    /// centre passes the end of its reachable tile the run is won with that tile's multiplier.
    /// </summary>
    public class RewardRamp : TrackElement
    {
        private const float TileHeight = 0.1f;
        private const float TileLengthFraction = 0.9f;
        private const float LabelHeight = 0.6f;
        private const float LabelCharacterSize = 0.12f;
        private static readonly int[] _upperMultipliers = { 8, 10, 20, 50, 100 };
        private static readonly Color _labelColor = new Color(0.12f, 0.1f, 0.2f, 1f);

        /// <summary>Number of tiles along +Z.</summary>
        public int TileCount = 10;

        /// <summary>Length of one tile along Z, in world units.</summary>
        public float TileLength = 2f;

        /// <summary>Every N surviving units lets the crowd reach one tile further.</summary>
        public int UnitsPerTile = 20;

        private GameConfig _config;
        private GameObject _visualRoot;
        private CrowdController _player;

        /// <summary>Multiplier for a tile index: 1, 2, 3, 4, 5, 8, 10, 20, 50, 100 (clamped at both ends).</summary>
        public int MultiplierForTile(int tile)
        {
            if (tile < 0)
            {
                tile = 0;
            }

            if (tile < 5)
            {
                return tile + 1;
            }

            int index = Math.Min(tile - 5, _upperMultipliers.Length - 1);
            return _upperMultipliers[index];
        }

        /// <summary>Sets the tile layout and makes the ramp span the whole lane.</summary>
        public void Configure(int tileCount, float tileLength, int unitsPerTile, GameConfig config)
        {
            if (config != null)
            {
                _config = config;
                HalfWidth = config.LaneHalfWidth;
            }

            TileCount = Mathf.Max(1, tileCount);
            TileLength = Mathf.Max(0.1f, tileLength);
            UnitsPerTile = Mathf.Max(1, unitsPerTile);
        }

        /// <summary>Lets the crowd run on; once the boss is dead, wins when its centre passes the end of the reachable tile.</summary>
        public override void OnCrowdOverlap(CrowdController player, float deltaTime)
        {
            if (Consumed || player == null)
            {
                return;
            }

            _player = player;
            Evaluate(player);
        }

        /// <summary>Flat tiles along +Z with "×N" labels; hue rises with the multiplier.</summary>
        public override void BuildVisuals(GameConfig config)
        {
            if (config != null)
            {
                _config = config;
                HalfWidth = config.LaneHalfWidth;
            }

            DestroyVisuals();

            _visualRoot = new GameObject("Visuals");
            _visualRoot.transform.SetParent(transform, false);
            Transform root = _visualRoot.transform;

            float laneHalfWidth = _config != null ? _config.LaneHalfWidth : HalfWidth;
            int tiles = Mathf.Max(1, TileCount);
            float tileLength = Mathf.Max(0.1f, TileLength);
            Mesh cube = TrackVisuals.CubeMesh();

            for (int i = 0; i < tiles; i++)
            {
                float t = tiles > 1 ? i / (float)(tiles - 1) : 0f;
                Color tileColor = Color.HSVToRGB(0.12f + 0.6f * t, 0.7f, 1f);
                float centreZ = (i + 0.5f) * tileLength;

                TrackVisuals.CreatePrimitive(
                    "Tile" + i, root, cube, TrackVisuals.SolidMaterial(tileColor),
                    new Vector3(0f, TileHeight * 0.5f, centreZ),
                    new Vector3(laneHalfWidth * 2f, TileHeight, tileLength * TileLengthFraction));

                TrackVisuals.CreateLabel(
                    root, GateMath.Label(GateOperation.Multiply, MultiplierForTile(i)), _labelColor,
                    LabelCharacterSize, new Vector3(0f, LabelHeight, centreZ));
            }
        }

        private void LateUpdate()
        {
            // The level runner stops polling elements more than 15 units behind the crowd, which
            // would make the far tiles unreachable; keep evaluating once the ramp has been entered.
            if (Consumed || _player == null || !_player.IsRunning)
            {
                return;
            }

            Evaluate(_player);
        }

        private void Evaluate(CrowdController player)
        {
            if (Consumed)
            {
                return;
            }

            // The boss drains the crowd without stopping it, so the crowd can already be several
            // tiles up the ramp while the boss is alive. Winning now would skip the RewardRamp state
            // and bank a multiplier mid-fight; keep running until the boss has died and
            // GameManager.EnterRewardRamp() has fired (or the crowd is wiped out and Lose() ends the run).
            GameManager game = GameManager.Instance;
            if (game != null && game.State == GameState.BossFight)
            {
                return;
            }

            int tile = ReachableTile(player.Count);
            float tileEndZ = Z + (tile + 1) * Mathf.Max(0.1f, TileLength);
            if (player.Position.z < tileEndZ)
            {
                return;
            }

            Consumed = true;
            player.IsRunning = false;

            if (game != null)
            {
                game.Win(MultiplierForTile(tile));
            }
        }

        private int ReachableTile(int playerCount)
        {
            int perTile = Mathf.Max(1, UnitsPerTile);
            int tiles = Mathf.Max(1, TileCount);
            int tile = Mathf.Max(0, playerCount) / perTile;
            return Mathf.Min(tiles - 1, tile);
        }

        private void DestroyVisuals()
        {
            if (_visualRoot != null)
            {
                Destroy(_visualRoot);
            }

            _visualRoot = null;
        }
    }
}

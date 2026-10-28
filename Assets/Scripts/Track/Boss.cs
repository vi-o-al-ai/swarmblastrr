using System;
using UnityEngine;
using Swarmblastrr.Core;
using Swarmblastrr.Crowd;
using Swarmblastrr.Meta;

namespace Swarmblastrr.Track
{
    /// <summary>
    /// The level's boss: a big capsule with an HP number. The first overlap switches the game to
    /// <c>GameState.BossFight</c>; every overlap frame it drains player units at
    /// <c>GameConfig.BossDrainRate</c> per second (never more than it needs to die) and loses
    /// <c>removed * UnitDamage</c> HP. At zero HP it hides itself and the game enters the reward ramp.
    /// </summary>
    public class Boss : TrackElement
    {
        private const float BodyScale = 3.5f;

        /// <summary>
        /// Local Y of the body centre. The built-in capsule is 2 units tall and centred on its
        /// origin, so its scaled half-height is exactly BodyScale; lifting it by that stands it on the floor.
        /// </summary>
        private const float BodyHeight = BodyScale;

        /// <summary>Local Y of the HP label, above the capsule top at BodyHeight + BodyScale.</summary>
        private const float LabelHeight = 8.2f;
        private const float LabelCharacterSize = 0.24f;
        private const float DefaultDrainRate = 60f;

        /// <summary>Gap kept between the boss centre and the crowd centre while the crowd is held, on top of half the crowd radius.</summary>
        private const float HoldGap = BodyScale * 0.5f;
        private static readonly Color _defaultBodyColor = new Color(0.85f, 0.7f, 0.45f);

        /// <summary>Hit points at full health.</summary>
        public int MaxHp { get; private set; }

        /// <summary>Remaining hit points.</summary>
        public int Hp { get; private set; }

        private GameConfig _config;
        private GameObject _visualRoot;
        private TextMesh _hpLabel;
        private CrowdController _player;
        private bool _fightStarted;
        private int _lastTickFrame = -1;

        /// <summary>Sets max HP (at least 1), fills HP and makes the boss span the whole lane.</summary>
        public void Configure(int maxHp, GameConfig config)
        {
            if (config != null)
            {
                _config = config;
                HalfWidth = config.LaneHalfWidth;
            }

            MaxHp = Mathf.Max(1, maxHp);
            Hp = MaxHp;
            _fightStarted = false;
            _lastTickFrame = -1;
            RefreshLabel();
        }

        /// <summary>
        /// Default overlap until the fight starts, except that the fight also begins as soon as the
        /// crowd centre reaches the hold line. For a crowd whose radius is under half the body scale
        /// the default front-reaches-Z rule fires after that line, and <c>ForwardLimitZ</c> never
        /// pulls the crowd back, so it would otherwise freeze with its front inside the body. Once
        /// the fight has started the boss stays engaged until it is consumed, so a crowd that
        /// shrinks below the contact distance cannot stall the fight.
        /// </summary>
        public override bool Overlaps(CrowdController player)
        {
            if (player == null)
            {
                return false;
            }

            if (_fightStarted && !Consumed && player == _player)
            {
                return true;
            }

            if (!_fightStarted && !Consumed)
            {
                float dx = Mathf.Abs(player.Position.x - transform.position.x);
                bool reachedHoldLine = player.Position.z >= HoldLineZ(player) && dx <= HalfWidth + player.Radius * 0.5f;
                if (reachedHoldLine)
                {
                    return true;
                }
            }

            return base.Overlaps(player);
        }

        /// <summary>Z at which the crowd centre is held during the fight: the boss centre less half the body and half the crowd radius.</summary>
        private float HoldLineZ(CrowdController player)
        {
            return Z - HoldGap - player.Radius * 0.5f;
        }

        /// <summary>Starts the fight on first contact, then drains the player and damages the boss.</summary>
        public override void OnCrowdOverlap(CrowdController player, float deltaTime)
        {
            if (Consumed || player == null)
            {
                return;
            }

            _player = player;
            Tick(player, deltaTime);
        }

        /// <summary>Scaled capsule in <c>GameConfig.BossColor</c> plus an HP label above it.</summary>
        public override void BuildVisuals(GameConfig config)
        {
            if (config != null)
            {
                _config = config;
            }

            DestroyVisuals();

            Color bodyColor = _config != null ? _config.BossColor : _defaultBodyColor;
            bodyColor.a = 1f;

            _visualRoot = new GameObject("Visuals");
            _visualRoot.transform.SetParent(transform, false);
            Transform root = _visualRoot.transform;

            TrackVisuals.CreatePrimitive(
                "Body", root, TrackVisuals.CapsuleMesh(), TrackVisuals.SolidMaterial(bodyColor),
                new Vector3(0f, BodyHeight, 0f), new Vector3(BodyScale, BodyScale, BodyScale));

            _hpLabel = TrackVisuals.CreateLabel(
                root, Hp.ToString(), Color.white, LabelCharacterSize, new Vector3(0f, LabelHeight, 0f));
        }

        private void LateUpdate()
        {
            // The level runner stops polling elements the crowd has left well behind. Once the
            // fight has started the boss keeps draining until it dies or the run ends, so a crowd
            // cannot simply outrun it.
            if (Consumed || !_fightStarted || _player == null || !_player.IsRunning)
            {
                return;
            }

            if (_lastTickFrame == Time.frameCount)
            {
                return;
            }

            if (!Overlaps(_player))
            {
                return;
            }

            Tick(_player, Time.deltaTime);
        }

        private void Tick(CrowdController player, float deltaTime)
        {
            _lastTickFrame = Time.frameCount;
            GameManager game = GameManager.Instance;

            if (!_fightStarted)
            {
                _fightStarted = true;
                // Hold the crowd against the boss for the duration of the fight so it cannot run past
                // the boss (and the reward ramp behind it) while the boss is still draining units.
                player.ForwardLimitZ = HoldLineZ(player);
                if (game != null)
                {
                    game.EnterBossFight();
                }
            }

            float drainRate = _config != null ? _config.BossDrainRate : DefaultDrainRate;
            int n = Mathf.Max(1, Mathf.RoundToInt(drainRate * deltaTime));

            int unitDamage = 1;
            if (game != null && game.Upgrades != null)
            {
                unitDamage = Mathf.Max(1, game.Upgrades.GetValueInt(UpgradeType.UnitDamage));
            }

            // Never drain more units than the boss needs to die this frame. The surplus would be
            // lost for nothing and, if it emptied the crowd, RemoveUnits would trigger Lose before
            // the kill is evaluated, making the outcome depend on the frame rate.
            long needed = ((long)Hp + unitDamage - 1) / unitDamage;
            n = (int)Math.Min(n, Math.Min(needed, (long)player.Count));
            if (n <= 0)
            {
                return;
            }

            int removed = player.RemoveUnits(n);
            if (removed <= 0)
            {
                return;
            }

            long damage = (long)removed * unitDamage;
            Hp = (int)Math.Max(0L, (long)Hp - damage);
            RefreshLabel();
            GameEvents.RaiseBossDamaged(Hp, MaxHp);

            if (Hp <= 0)
            {
                Consumed = true;
                HideVisuals();
                player.ForwardLimitZ = float.PositiveInfinity;
                if (game != null)
                {
                    game.EnterRewardRamp();
                }
            }
        }

        private void RefreshLabel()
        {
            if (_hpLabel != null)
            {
                _hpLabel.text = Hp.ToString();
            }
        }

        private void HideVisuals()
        {
            if (_visualRoot != null)
            {
                _visualRoot.SetActive(false);
            }
        }

        private void DestroyVisuals()
        {
            if (_visualRoot != null)
            {
                Destroy(_visualRoot);
            }

            _visualRoot = null;
            _hpLabel = null;
        }
    }
}

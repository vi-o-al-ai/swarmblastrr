using System;
using UnityEngine;
using Swarmblastrr.Track;

namespace Swarmblastrr.Core
{
    /// <summary>
    /// Static event hub used by every subsystem to broadcast gameplay changes without holding
    /// references to each other. Subscribers must unsubscribe in OnDestroy/OnDisable.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>Raised after <see cref="GameManager"/> changes its state.</summary>
        public static event Action<GameState> StateChanged;

        /// <summary>Raised when the player's logical unit count changes: (previous, current).</summary>
        public static event Action<int, int> CountChanged;

        /// <summary>Raised when the player crowd passes through a gate: (op, before, after, worldPos).</summary>
        public static event Action<GateOperation, int, int, Vector3> GateHit;

        /// <summary>Raised when the coin total changes; carries the new total.</summary>
        public static event Action<int> CoinsChanged;

        /// <summary>Raised after an upgrade level changes.</summary>
        public static event Action UpgradesChanged;

        /// <summary>Raised when the boss takes damage: (hp, maxHp).</summary>
        public static event Action<int, int> BossDamaged;

        /// <summary>Raised once per run when it ends, won or lost.</summary>
        public static event Action<LevelResult> LevelFinished;

        /// <summary>Null-safe raise of <see cref="StateChanged"/>.</summary>
        public static void RaiseStateChanged(GameState state)
        {
            Action<GameState> handler = StateChanged;
            if (handler != null)
            {
                handler(state);
            }
        }

        /// <summary>Null-safe raise of <see cref="CountChanged"/>.</summary>
        public static void RaiseCountChanged(int previous, int current)
        {
            Action<int, int> handler = CountChanged;
            if (handler != null)
            {
                handler(previous, current);
            }
        }

        /// <summary>Null-safe raise of <see cref="GateHit"/>.</summary>
        public static void RaiseGateHit(GateOperation op, int before, int after, Vector3 worldPos)
        {
            Action<GateOperation, int, int, Vector3> handler = GateHit;
            if (handler != null)
            {
                handler(op, before, after, worldPos);
            }
        }

        /// <summary>Null-safe raise of <see cref="CoinsChanged"/>.</summary>
        public static void RaiseCoinsChanged(int total)
        {
            Action<int> handler = CoinsChanged;
            if (handler != null)
            {
                handler(total);
            }
        }

        /// <summary>Null-safe raise of <see cref="UpgradesChanged"/>.</summary>
        public static void RaiseUpgradesChanged()
        {
            Action handler = UpgradesChanged;
            if (handler != null)
            {
                handler();
            }
        }

        /// <summary>Null-safe raise of <see cref="BossDamaged"/>.</summary>
        public static void RaiseBossDamaged(int hp, int maxHp)
        {
            Action<int, int> handler = BossDamaged;
            if (handler != null)
            {
                handler(hp, maxHp);
            }
        }

        /// <summary>Null-safe raise of <see cref="LevelFinished"/>.</summary>
        public static void RaiseLevelFinished(LevelResult result)
        {
            Action<LevelResult> handler = LevelFinished;
            if (handler != null)
            {
                handler(result);
            }
        }

        /// <summary>
        /// Drops every subscriber. Called by <see cref="GameManager"/> in OnDestroy and by tests so
        /// stale listeners never survive a scene reload or a test run.
        /// </summary>
        public static void ClearAll()
        {
            StateChanged = null;
            CountChanged = null;
            GateHit = null;
            CoinsChanged = null;
            UpgradesChanged = null;
            BossDamaged = null;
            LevelFinished = null;
        }
    }
}

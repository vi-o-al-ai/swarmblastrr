using UnityEngine;
using Swarmblastrr.Core;

namespace Swarmblastrr.Meta
{
    /// <summary>
    /// Owns the player's <see cref="SaveData"/> at runtime: answers upgrade level/value/cost queries,
    /// sells upgrades and tracks coins. Purchases save immediately; <see cref="AddCoins"/> does not,
    /// because <see cref="GameManager"/> saves once at the end of a run.
    /// </summary>
    [DisallowMultipleComponent]
    public class UpgradeManager : MonoBehaviour
    {
        /// <summary>The live save record. Null until <see cref="Initialize"/> has run.</summary>
        public SaveData Data { get; private set; }

        /// <summary>Current coin balance (0 before initialisation).</summary>
        public int Coins => Data == null ? 0 : Data.Coins;

        /// <summary>
        /// Adopts <paramref name="data"/> as the live record, or loads one through
        /// <see cref="SaveSystem.Load"/> when null is passed. Raises <see cref="GameEvents.CoinsChanged"/>
        /// and <see cref="GameEvents.UpgradesChanged"/> so any UI already listening syncs up.
        /// </summary>
        public void Initialize(SaveData data)
        {
            if (data == null)
            {
                data = SaveSystem.Load();
            }

            data.EnsureArrays();
            Data = data;

            GameEvents.RaiseCoinsChanged(Data.Coins);
            GameEvents.RaiseUpgradesChanged();
        }

        /// <summary>Owned level of <paramref name="type"/>; 0 when unknown.</summary>
        public int GetLevel(UpgradeType type)
        {
            EnsureData();

            int index = (int)type;
            if (index < 0 || index >= Data.UpgradeLevels.Length)
            {
                return 0;
            }

            return Data.UpgradeLevels[index];
        }

        /// <summary>Effective value of <paramref name="type"/> at its current level.</summary>
        public float GetValue(UpgradeType type)
        {
            return UpgradeMath.ValueFor(UpgradeCatalog.Get(type), GetLevel(type));
        }

        /// <summary><see cref="GetValue"/> rounded to the nearest integer.</summary>
        public int GetValueInt(UpgradeType type)
        {
            return Mathf.RoundToInt(GetValue(type));
        }

        /// <summary>Coins needed to buy the next level of <paramref name="type"/>.</summary>
        public int GetCost(UpgradeType type)
        {
            return UpgradeMath.CostFor(UpgradeCatalog.Get(type), GetLevel(type));
        }

        /// <summary>True when <paramref name="type"/> is not maxed and the player can afford the next level.</summary>
        public bool CanBuy(UpgradeType type)
        {
            EnsureData();

            UpgradeDefinition def = UpgradeCatalog.Get(type);
            int level = GetLevel(type);
            if (UpgradeMath.IsMaxed(def, level))
            {
                return false;
            }

            return Data.Coins >= UpgradeMath.CostFor(def, level);
        }

        /// <summary>
        /// Buys one level of <paramref name="type"/> if <see cref="CanBuy"/> allows it: deducts the cost,
        /// increments the level, saves, and raises <see cref="GameEvents.CoinsChanged"/> then
        /// <see cref="GameEvents.UpgradesChanged"/>. Returns false and changes nothing otherwise.
        /// </summary>
        public bool TryBuy(UpgradeType type)
        {
            if (!CanBuy(type))
            {
                return false;
            }

            UpgradeDefinition def = UpgradeCatalog.Get(type);
            int index = (int)type;
            int level = Data.UpgradeLevels[index];
            int cost = UpgradeMath.CostFor(def, level);

            int remaining = Data.Coins - cost;
            if (remaining < 0)
            {
                remaining = 0;
            }

            Data.Coins = remaining;
            Data.UpgradeLevels[index] = level + 1;

            Save();
            GameEvents.RaiseCoinsChanged(Data.Coins);
            GameEvents.RaiseUpgradesChanged();
            return true;
        }

        /// <summary>
        /// Adds <paramref name="amount"/> coins (negative amounts spend, never below 0) and raises
        /// <see cref="GameEvents.CoinsChanged"/>. Does not save; the caller decides when to persist.
        /// </summary>
        public void AddCoins(int amount)
        {
            EnsureData();

            long total = (long)Data.Coins + amount;
            if (total < 0L)
            {
                total = 0L;
            }
            else if (total > int.MaxValue)
            {
                total = int.MaxValue;
            }

            Data.Coins = (int)total;
            GameEvents.RaiseCoinsChanged(Data.Coins);
        }

        /// <summary>Persists <see cref="Data"/> through <see cref="SaveSystem.Save"/>.</summary>
        public void Save()
        {
            if (Data == null)
            {
                return;
            }

            SaveSystem.Save(Data);
        }

        private void EnsureData()
        {
            if (Data == null)
            {
                Initialize(null);
            }
        }
    }
}

using System;

namespace Swarmblastrr.Meta
{
    /// <summary>
    /// Everything that persists between sessions: coins, progress and upgrade levels. Serialized with
    /// <c>JsonUtility</c>, so only public fields are stored. Keep it a plain class with field
    /// initialisers so a partially written or older JSON file still deserialises to sane defaults.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>Schema version of this record; bump when the layout changes incompatibly.</summary>
        public int Version = 1;

        /// <summary>Current coin balance. Never negative.</summary>
        public int Coins;

        /// <summary>Zero-based index of the next level to play.</summary>
        public int LevelIndex;

        /// <summary>Owned level of each upgrade, indexed by <c>(int)UpgradeType</c>.</summary>
        public int[] UpgradeLevels = new int[3];

        /// <summary>
        /// Creates a fresh record with zero coins, level 0 and every upgrade at level 0.
        /// </summary>
        public static SaveData CreateNew()
        {
            SaveData data = new SaveData();
            data.EnsureArrays();
            return data;
        }

        /// <summary>
        /// Guarantees <see cref="UpgradeLevels"/> is non-null and long enough for every entry in
        /// <see cref="UpgradeCatalog.All"/>, so a save written by an older build keeps working after new
        /// upgrades are appended. Also clamps negative values (from a tampered or corrupt file) back to 0.
        /// </summary>
        public void EnsureArrays()
        {
            int required = UpgradeCatalog.All.Length;

            if (UpgradeLevels == null)
            {
                UpgradeLevels = new int[required];
            }
            else if (UpgradeLevels.Length < required)
            {
                int[] grown = new int[required];
                Array.Copy(UpgradeLevels, grown, UpgradeLevels.Length);
                UpgradeLevels = grown;
            }

            for (int i = 0; i < UpgradeLevels.Length; i++)
            {
                if (UpgradeLevels[i] < 0)
                {
                    UpgradeLevels[i] = 0;
                }
            }

            if (Coins < 0)
            {
                Coins = 0;
            }

            if (LevelIndex < 0)
            {
                LevelIndex = 0;
            }

            if (Version < 1)
            {
                Version = 1;
            }
        }
    }
}

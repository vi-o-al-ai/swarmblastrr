using System;
using UnityEngine;

namespace Swarmblastrr.Meta
{
    /// <summary>
    /// Loads and saves <see cref="SaveData"/> through a swappable <see cref="ISaveStore"/>. Loading never
    /// throws: a missing, empty or corrupt record yields a fresh <see cref="SaveData.CreateNew"/>.
    /// </summary>
    public static class SaveSystem
    {
        private static ISaveStore _store;

        /// <summary>
        /// The backing store. Defaults lazily to a <see cref="FileSaveStore"/> at
        /// <see cref="FileSaveStore.DefaultPath"/>; tests assign a <see cref="MemorySaveStore"/>.
        /// Assigning null restores the lazy default.
        /// </summary>
        public static ISaveStore Store
        {
            get
            {
                if (_store == null)
                {
                    _store = new FileSaveStore(FileSaveStore.DefaultPath);
                }

                return _store;
            }
            set
            {
                _store = value;
            }
        }

        /// <summary>
        /// Reads and deserializes the stored record. Returns a new record when nothing is stored or the
        /// JSON cannot be parsed, and always returns data that has passed <see cref="SaveData.EnsureArrays"/>.
        /// </summary>
        public static SaveData Load()
        {
            SaveData data = null;

            try
            {
                ISaveStore store = Store;
                string json = store.Exists ? store.Read() : null;
                if (!string.IsNullOrEmpty(json))
                {
                    data = JsonUtility.FromJson<SaveData>(json);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Swarmblastrr: save data could not be read, starting fresh: " + ex.Message);
                data = null;
            }

            if (data == null)
            {
                data = SaveData.CreateNew();
            }

            data.EnsureArrays();
            return data;
        }

        /// <summary>
        /// Serializes <paramref name="data"/> with <c>JsonUtility.ToJson(data, true)</c> and writes it to
        /// the store. A null record is ignored. Never throws.
        /// </summary>
        public static void Save(SaveData data)
        {
            if (data == null)
            {
                return;
            }

            try
            {
                data.EnsureArrays();
                string json = JsonUtility.ToJson(data, true);
                Store.Write(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Swarmblastrr: save data could not be written: " + ex.Message);
            }
        }

        /// <summary>Deletes the stored record so the next <see cref="Load"/> starts fresh.</summary>
        public static void Reset()
        {
            try
            {
                Store.Delete();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Swarmblastrr: save data could not be deleted: " + ex.Message);
            }
        }
    }
}

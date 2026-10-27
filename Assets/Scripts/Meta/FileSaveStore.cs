using System;
using System.IO;
using UnityEngine;

namespace Swarmblastrr.Meta
{
    /// <summary>
    /// <see cref="ISaveStore"/> backed by a single JSON file on disk. Writes create the parent directory
    /// on demand and never throw: an IO failure is logged as a warning and the previous file is left
    /// as it was.
    /// </summary>
    public sealed class FileSaveStore : ISaveStore
    {
        private readonly string _path;

        /// <summary>
        /// Creates a store that reads and writes <paramref name="path"/>.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or empty.</exception>
        public FileSaveStore(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Save path must not be null or empty.", nameof(path));
            }

            _path = path;
        }

        /// <summary>The platform's persistent data folder plus <c>save.json</c>.</summary>
        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "save.json");

        /// <summary>Full path of the file this store uses.</summary>
        public string FilePath => _path;

        /// <inheritdoc />
        public bool Exists => File.Exists(_path);

        /// <summary>
        /// Returns the file contents, or null when the file does not exist. Other IO errors propagate to
        /// the caller; <see cref="SaveSystem.Load"/> catches them.
        /// </summary>
        public string Read()
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            return File.ReadAllText(_path);
        }

        /// <inheritdoc />
        public void Write(string json)
        {
            try
            {
                string directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(_path, json ?? string.Empty);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Swarmblastrr: could not write save file '" + _path + "': " + ex.Message);
            }
        }

        /// <inheritdoc />
        public void Delete()
        {
            try
            {
                if (File.Exists(_path))
                {
                    File.Delete(_path);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Swarmblastrr: could not delete save file '" + _path + "': " + ex.Message);
            }
        }
    }
}

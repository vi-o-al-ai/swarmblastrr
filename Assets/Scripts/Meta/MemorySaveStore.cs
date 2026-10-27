namespace Swarmblastrr.Meta
{
    /// <summary>
    /// <see cref="ISaveStore"/> that keeps the JSON in a field. Used by edit-mode tests and by anyone who
    /// wants a throwaway profile without touching the disk.
    /// </summary>
    public sealed class MemorySaveStore : ISaveStore
    {
        private string _json;

        /// <summary>Creates an empty store.</summary>
        public MemorySaveStore()
        {
        }

        /// <summary>Creates a store pre-loaded with <paramref name="initialJson"/> (null leaves it empty).</summary>
        public MemorySaveStore(string initialJson)
        {
            _json = initialJson;
        }

        /// <inheritdoc />
        public bool Exists => _json != null;

        /// <inheritdoc />
        public string Read()
        {
            return _json;
        }

        /// <inheritdoc />
        public void Write(string json)
        {
            _json = json ?? string.Empty;
        }

        /// <inheritdoc />
        public void Delete()
        {
            _json = null;
        }
    }
}

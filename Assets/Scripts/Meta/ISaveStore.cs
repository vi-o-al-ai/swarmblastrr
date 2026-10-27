namespace Swarmblastrr.Meta
{
    /// <summary>
    /// Where serialized save JSON lives. <see cref="SaveSystem"/> talks only to this interface so the
    /// game can persist to disk while tests use an in-memory store.
    /// </summary>
    public interface ISaveStore
    {
        /// <summary>True when a record has been written and not deleted.</summary>
        bool Exists { get; }

        /// <summary>Returns the stored JSON, or null when nothing is stored.</summary>
        string Read();

        /// <summary>Replaces the stored JSON.</summary>
        void Write(string json);

        /// <summary>Removes the stored record, if any.</summary>
        void Delete();
    }
}

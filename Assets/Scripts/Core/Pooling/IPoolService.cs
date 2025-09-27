using UnityEngine;

namespace Thrustslinger.Core
{
    /// <summary>
    /// Runtime pooling service contract.
    /// </summary>
    public interface IPoolService
    {
        /// <summary>
        /// Fetch an instance associated with <paramref name="key"/> and return the requested component.
        /// </summary>
        T Get<T>(string key = null, object context = null) where T : Component;

        /// <summary>
        /// Fetch an instance associated with <paramref name="key"/>.
        /// </summary>
        GameObject Get(string key = null, object context = null);

        /// <summary>
        /// Return an instance to its originating pool.
        /// </summary>
        void Release(object instance);

        /// <summary>
        /// Ensure the pool has at least <paramref name="count"/> warmed instances available.
        /// </summary>
        void Prewarm(string key, int count);

        /// <summary>
        /// Register a prefab against a key and optionally prewarm instances.
        /// </summary>
        void RegisterPrefab(string key, GameObject prefab, int initialSize = 0, Transform containerParent = null);

        /// <summary>
        /// True if the service contains a pool for <paramref name="key"/>.
        /// </summary>
        bool Contains(string key);
    }
}

namespace Thrustslinger.Core
{
    /// <summary>
    /// Contract for objects managed by the runtime pool.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// Called when the object is revived from the pool.
        /// </summary>
        /// <param name="context">Optional caller-provided context for spawn initialization.</param>
        void OnSpawned(object context);

        /// <summary>
        /// Called when the object is returned to the pool.
        /// </summary>
        void OnDespawned();
    }
}

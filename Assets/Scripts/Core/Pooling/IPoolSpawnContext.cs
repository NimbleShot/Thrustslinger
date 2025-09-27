using UnityEngine;

namespace Thrustslinger.Core
{
    /// <summary>
    /// Optional spawn transform context that can be supplied to pooled spawns.
    /// </summary>
    public interface IPoolSpawnContext
    {
        void ApplySpawnTransform(Transform instanceTransform);
    }
}

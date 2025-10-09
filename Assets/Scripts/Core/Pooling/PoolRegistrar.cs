using UnityEngine;

namespace Thrustslinger.Core
{
    /// <summary>
    /// Centralized pool registration component. Attach to the ObjectPool GameObject alongside PoolService.
    /// Registers all pooled prefabs at startup with configured prewarm counts.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PoolService))]
    public sealed class PoolRegistrar : MonoBehaviour
    {
        [System.Serializable]
        public class PoolEntry
        {
            [Tooltip("Unique key used to retrieve objects from the pool")]
            public string poolKey = "default";
            
            [Tooltip("Prefab to pool (must have IPoolable component or will be added PooledObject at runtime)")]
            public GameObject prefab;
            
            [Tooltip("Number of instances to pre-create at startup")]
            [Min(0)] public int prewarmCount = 10;
            
            [Tooltip("Optional parent transform for this pool's container (null = use PoolService root)")]
            public Transform containerParent;
        }

        [Header("Pool Registrations")]
        [SerializeField] private PoolEntry[] pools = new PoolEntry[]
        {
            new PoolEntry { poolKey = "projectiles.default", prewarmCount = 16 },
            new PoolEntry { poolKey = "targets.default", prewarmCount = 8 }
        };

        [Header("Debug")]
        [SerializeField] private bool logRegistrations = true;

        private void Awake()
        {
            RegisterAllPools();
        }

        private void RegisterAllPools()
        {
            var poolService = PoolService.Instance;
            if (poolService == null)
            {
                Debug.LogError("[PoolRegistrar] PoolService instance not found. Ensure PoolService is attached to this GameObject.", this);
                return;
            }

            int registered = 0;
            int skipped = 0;

            foreach (var entry in pools)
            {
                if (string.IsNullOrWhiteSpace(entry.poolKey))
                {
                    Debug.LogWarning("[PoolRegistrar] Skipping pool entry with empty key.", this);
                    skipped++;
                    continue;
                }

                if (entry.prefab == null)
                {
                    Debug.LogWarning($"[PoolRegistrar] Skipping pool '{entry.poolKey}': prefab is null.", this);
                    skipped++;
                    continue;
                }

                if (poolService.Contains(entry.poolKey))
                {
                    if (logRegistrations)
                    {
                        Debug.Log($"[PoolRegistrar] Pool '{entry.poolKey}' already registered (pre-warmed from previous scene or duplicate). Skipping.", this);
                    }
                    skipped++;
                    continue;
                }

                poolService.RegisterPrefab(
                    entry.poolKey,
                    entry.prefab,
                    entry.prewarmCount,
                    entry.containerParent
                );

                registered++;

                if (logRegistrations)
                {
                    Debug.Log($"[PoolRegistrar] Registered pool '{entry.poolKey}' with {entry.prewarmCount} prewarmed instances.", this);
                }
            }

            if (logRegistrations)
            {
                Debug.Log($"[PoolRegistrar] Registration complete: {registered} registered, {skipped} skipped.", this);
            }
        }

        [ContextMenu("Re-register All Pools")]
        private void ReregisterPools()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[PoolRegistrar] Re-registration only available at runtime.", this);
                return;
            }

            RegisterAllPools();
        }

        private void OnValidate()
        {
            // Check for duplicate keys
            if (pools == null || pools.Length == 0) return;

            var keys = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < pools.Length; i++)
            {
                var entry = pools[i];
                if (string.IsNullOrWhiteSpace(entry.poolKey)) continue;

                if (!keys.Add(entry.poolKey))
                {
                    Debug.LogWarning($"[PoolRegistrar] Duplicate pool key detected: '{entry.poolKey}'. Each pool must have a unique key.", this);
                }
            }
        }
    }
}

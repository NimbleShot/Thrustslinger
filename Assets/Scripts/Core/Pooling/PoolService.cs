using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Thrustslinger.Core
{
    /// <summary>
    /// Centralised runtime pooling service. Ensures no Instantiate/Destroy calls in the arena loop.
    /// </summary>
    public sealed class PoolService : Singleton<PoolService>, IPoolService
    {
        private sealed class PoolEntry
        {
            public string Key;
            public GameObject Prefab;
            public Transform Container;
            public readonly Queue<PooledObject> Available = new();
            public readonly HashSet<PooledObject> InUse = new();
        }

        private readonly Dictionary<string, PoolEntry> _pools = new(StringComparer.Ordinal);
        private readonly Dictionary<GameObject, PoolEntry> _lookup = new();
        private string _defaultKey;

        private Transform _rootContainer;

        private void Awake()
        {
            if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            gameObject.name = "[PoolService]";
            DontDestroyOnLoad(gameObject);
            EnsureRootContainer();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // When a new scene loads in Single mode, clear all pools since objects are destroyed
            if (mode == LoadSceneMode.Single)
            {
                ClearAllPools();
            }
        }

        /// <summary>
        /// Clears all pooled object references but keeps pool registrations. Called when scenes are loaded.
        /// </summary>
        public void ClearAllPools()
        {
            foreach (var entry in _pools.Values)
            {
                // First, deactivate and return all in-use objects to prevent them appearing in next scene
                var inUseList = new List<PooledObject>(entry.InUse);
                foreach (var pooled in inUseList)
                {
                    if (pooled != null && pooled.gameObject != null)
                    {
                        pooled.gameObject.SetActive(false);
                        pooled.InvokeDespawned();
                    }
                }
                
                // Clear queues and sets - objects are already destroyed by scene unload
                entry.Available.Clear();
                entry.InUse.Clear();
                
                // Destroy the container if it still exists (it might be in a destroyed scene)
                if (entry.Container != null)
                {
                    Destroy(entry.Container.gameObject);
                    entry.Container = null;
                }
            }
            
            _lookup.Clear();
            // NOTE: We keep _pools and _defaultKey intact so registrations persist
            // The containers will be recreated when objects are spawned again
        }

        public bool Contains(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && _pools.ContainsKey(key);
        }

        public void RegisterPrefab(string key, GameObject prefab, int initialSize = 0, Transform containerParent = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogError("[PoolService] Cannot register a prefab with an empty key.", this);
                return;
            }

            if (prefab == null)
            {
                Debug.LogError("[PoolService] Cannot register a null prefab.", this);
                return;
            }

            if (_pools.TryGetValue(key, out var existing))
            {
                if (existing.Prefab != prefab)
                {
                    Debug.LogWarning($"[PoolService] Pool '{key}' already registered with a different prefab. Keeping original registration.", this);
                }

                if (initialSize > 0)
                {
                    Prewarm(key, initialSize);
                }

                return;
            }

            var container = new GameObject($"{key}_Pool").transform;
            container.SetParent(containerParent != null ? containerParent : EnsureRootContainer(), false);
            container.gameObject.hideFlags = HideFlags.DontSave;

            var entry = new PoolEntry
            {
                Key = key,
                Prefab = prefab,
                Container = container
            };

            _pools.Add(key, entry);
            if (string.IsNullOrEmpty(_defaultKey))
            {
                _defaultKey = key;
            }

            if (initialSize > 0)
            {
                Prewarm(key, initialSize);
            }
        }

        public void Prewarm(string key, int count)
        {
            if (count <= 0) return;
            if (!TryGetEntry(key, out var entry)) return;

            var deficit = Mathf.Max(0, count - entry.Available.Count);
            for (int i = 0; i < deficit; i++)
            {
                var pooled = CreateInstance(entry);
                entry.Available.Enqueue(pooled);
            }
        }

        public GameObject Get(string key = null, object context = null)
        {
            var entry = ResolveEntry(key);
            if (entry == null) return null;

            // Clean out any destroyed objects from the queue
            while (entry.Available.Count > 0)
            {
                var pooled = entry.Available.Peek();
                if (pooled == null || pooled.gameObject == null)
                {
                    entry.Available.Dequeue();
                    continue;
                }
                break;
            }

            if (entry.Available.Count == 0)
            {
                entry.Available.Enqueue(CreateInstance(entry));
            }

            var validPooled = entry.Available.Dequeue();
            
            // Final safety check
            if (validPooled == null || validPooled.gameObject == null)
            {
                Debug.LogWarning($"[PoolService] Dequeued a destroyed pooled object from '{key}'. Creating a new instance.", this);
                validPooled = CreateInstance(entry);
            }
            
            entry.InUse.Add(validPooled);

            var instance = validPooled.gameObject;
            instance.transform.SetParent(null, false);

            if (context is IPoolSpawnContext spawnContext)
            {
                spawnContext.ApplySpawnTransform(instance.transform);
            }

            instance.SetActive(true);
            validPooled.InvokeSpawned(context);

            return instance;
        }

        public T Get<T>(string key = null, object context = null) where T : Component
        {
            var instance = Get(key, context);
            if (instance == null) return null;

            if (!instance.TryGetComponent(out T component))
            {
                Debug.LogError($"[PoolService] Instance from pool '{key ?? _defaultKey}' is missing component {typeof(T).Name}.", instance);
            }

            return component;
        }

        public void Release(object instance)
        {
            if (instance == null) return;

            GameObject go = null;
            switch (instance)
            {
                case GameObject gameObject:
                    go = gameObject;
                    break;
                case Component component:
                    go = component.gameObject;
                    break;
            }

            if (go == null)
            {
                Debug.LogWarning("[PoolService] Release called with an unsupported instance type.", this);
                return;
            }

            if (!_lookup.TryGetValue(go, out var entry))
            {
                go.SetActive(false);
                return;
            }

            if (!go.TryGetComponent(out PooledObject pooled))
            {
                go.SetActive(false);
                return;
            }

            if (!entry.InUse.Remove(pooled))
            {
                return;
            }

            go.SetActive(false);
            pooled.InvokeDespawned();
            go.transform.SetParent(entry.Container, false);
            entry.Available.Enqueue(pooled);
        }

        private PoolEntry ResolveEntry(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                if (!string.IsNullOrEmpty(_defaultKey))
                {
                    key = _defaultKey;
                }
                else
                {
                    Debug.LogError("[PoolService] No pools registered yet; cannot fetch without a key.", this);
                    return null;
                }
            }

            if (!_pools.TryGetValue(key, out var entry))
            {
                Debug.LogError($"[PoolService] No pool registered for key '{key}'.", this);
                return null;
            }

            return entry;
        }

        private bool TryGetEntry(string key, out PoolEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(key))
            {
                key = _defaultKey;
            }

            if (!_pools.TryGetValue(key, out entry))
            {
                Debug.LogWarning($"[PoolService] Attempting to access unregistered pool '{key}'.", this);
                return false;
            }

            return true;
        }

        private PooledObject CreateInstance(PoolEntry entry)
        {
            // Recreate container if it was destroyed (e.g., after scene transition)
            if (entry.Container == null)
            {
                var container = new GameObject($"{entry.Key}_Pool").transform;
                container.SetParent(EnsureRootContainer(), false);
                container.gameObject.hideFlags = HideFlags.DontSave;
                entry.Container = container;
            }
            
            var go = Instantiate(entry.Prefab, entry.Container);
            go.name = $"{entry.Prefab.name}_Pooled";
            var pooled = go.GetComponent<PooledObject>();
            if (pooled == null)
            {
                pooled = go.AddComponent<PooledObject>();
            }

            pooled.Initialize(entry.Key);
            pooled.RefreshPoolableCache();
            go.SetActive(false);

            if (!_lookup.ContainsKey(go))
            {
                _lookup.Add(go, entry);
            }
            else
            {
                _lookup[go] = entry;
            }

            return pooled;
        }

        private Transform EnsureRootContainer()
        {
            if (_rootContainer == null)
            {
                var root = new GameObject("[Pools]");
                root.transform.SetParent(transform, false);
                root.hideFlags = HideFlags.DontSave;
                _rootContainer = root.transform;
            }

            return _rootContainer;
        }
    }
}

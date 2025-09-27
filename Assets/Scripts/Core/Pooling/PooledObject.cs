using UnityEngine;

namespace Thrustslinger.Core
{
    [DisallowMultipleComponent]
    public sealed class PooledObject : MonoBehaviour
    {
        public string Key { get; private set; }

        private IPoolable _poolable;

        public void Initialize(string key)
        {
            Key = key;
            RefreshPoolableCache();
        }

        public void RefreshPoolableCache()
        {
            if (_poolable == null)
            {
                _poolable = GetComponent<IPoolable>();
            }
        }

        public void InvokeSpawned(object context)
        {
            _poolable?.OnSpawned(context);
        }

        public void InvokeDespawned()
        {
            _poolable?.OnDespawned();
        }
    }
}

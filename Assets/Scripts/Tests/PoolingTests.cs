#if UNITY_EDITOR
using NUnit.Framework;
using Thrustslinger.Core;
using UnityEngine;

namespace Thrustslinger.Tests
{
    public sealed class DummyPoolable : MonoBehaviour, IPoolable
    {
        public int SpawnCount { get; private set; }
        public int DespawnCount { get; private set; }
        public object LastContext { get; private set; }

        public void OnSpawned(object context)
        {
            SpawnCount++;
            LastContext = context;
        }

        public void OnDespawned()
        {
            DespawnCount++;
        }
    }

    public class PoolingTests
    {
        private const string Key = "tests.dummy";
        private GameObject _prefab;

        [SetUp]
        public void SetUp()
        {
            // Ensure a clean scene root for each test
            var existing = Object.FindFirstObjectByType<PoolService>();
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            _prefab = new GameObject("DummyPrefab");
            _prefab.AddComponent<DummyPoolable>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_prefab != null)
            {
                Object.DestroyImmediate(_prefab);
            }

            var service = Object.FindFirstObjectByType<PoolService>();
            if (service != null)
            {
                Object.DestroyImmediate(service.gameObject);
            }
        }

        [Test]
        public void Prewarm_Get_Release_ReusesInstances()
        {
            PoolService.Instance.RegisterPrefab(Key, _prefab, 0, null);
            PoolService.Instance.Prewarm(Key, 2);

            // First get
            var a = PoolService.Instance.Get<DummyPoolable>(Key);
            Assert.IsNotNull(a);
            Assert.IsTrue(a.gameObject.activeSelf);
            int aSpawnCount = a.SpawnCount;

            // Second get
            var b = PoolService.Instance.Get<DummyPoolable>(Key);
            Assert.IsNotNull(b);
            Assert.IsTrue(b.gameObject.activeSelf);
            Assert.AreNotSame(a.gameObject, b.gameObject);

            // Release both and ensure they're inactive again
            PoolService.Instance.Release(a);
            PoolService.Instance.Release(b);

            Assert.IsFalse(a.gameObject.activeSelf);
            Assert.IsFalse(b.gameObject.activeSelf);
            Assert.GreaterOrEqual(a.DespawnCount, 1);
            Assert.GreaterOrEqual(b.DespawnCount, 1);

            // Getting again should reactivate and call OnSpawned again
            var a2 = PoolService.Instance.Get<DummyPoolable>(Key);
            Assert.AreSame(a.gameObject, a2.gameObject);
            Assert.Greater(a2.SpawnCount, aSpawnCount);
        }

        private struct SimpleSpawnContext : IPoolSpawnContext
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Transform Parent;
            public void ApplySpawnTransform(Transform t)
            {
                t.SetParent(Parent, false);
                t.SetPositionAndRotation(Position, Rotation);
            }
        }

        [Test]
        public void Spawn_Context_AppliesTransform()
        {
            PoolService.Instance.RegisterPrefab(Key, _prefab, 1, null);

            var parent = new GameObject("Parent").transform;
            var pos = new Vector3(3, 1, -2);
            var rot = Quaternion.Euler(0, 45, 0);
            var ctx = new SimpleSpawnContext { Position = pos, Rotation = rot, Parent = parent };

            var inst = PoolService.Instance.Get<DummyPoolable>(Key, ctx);
            Assert.IsNotNull(inst);
            Assert.AreEqual(parent, inst.transform.parent);
            Assert.That(Vector3.Distance(inst.transform.position, pos), Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(inst.transform.rotation, rot), Is.LessThan(1e-3f));

            PoolService.Instance.Release(inst);
            Object.DestroyImmediate(parent.gameObject);
        }
    }
}

#endif
